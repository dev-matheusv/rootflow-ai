using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Application.Auth.Dtos;
using RootFlow.Application.Workspaces.Commands;
using RootFlow.Application.Workspaces.Dtos;
using RootFlow.Application.Workspaces.Queries;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Workspaces;

public sealed class WorkspaceCollaborationService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private const string InviteSuccessMessage = "Invite sent. If the address can access this workspace, they can use the link to join.";
    private const string InvalidInvitationMessage = "This workspace invite is invalid or has expired.";
    private readonly IAuthRepository _authRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;
    private readonly IWorkspaceInvitationRepository _workspaceInvitationRepository;
    private readonly IWorkspaceInvitationNotifier _workspaceInvitationNotifier;
    private readonly IClock _clock;

    public WorkspaceCollaborationService(
        IAuthRepository authRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository,
        IWorkspaceInvitationRepository workspaceInvitationRepository,
        IWorkspaceInvitationNotifier workspaceInvitationNotifier,
        IClock clock)
    {
        _authRepository = authRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
        _workspaceInvitationRepository = workspaceInvitationRepository;
        _workspaceInvitationNotifier = workspaceInvitationNotifier;
        _clock = clock;
    }

    public async Task<WorkspaceInvitationResultDto> InviteAsync(
        InviteWorkspaceMemberCommand command,
        CancellationToken cancellationToken = default)
    {
        var inviterMembership = await RequireWorkspaceAccessAsync(
            command.WorkspaceId,
            command.InvitedByUserId,
            cancellationToken);

        if (inviterMembership.Role is not (WorkspaceRole.Owner or WorkspaceRole.Admin))
        {
            throw new WorkspaceAccessDeniedException("Only workspace owners and admins can send invites.");
        }

        var workspace = await _workspaceRepository.GetByIdAsync(command.WorkspaceId, cancellationToken);
        if (workspace is null || !workspace.IsActive)
        {
            throw new WorkspaceInviteConflictException("The workspace could not be found.");
        }

        var inviter = await _authRepository.GetUserByIdAsync(command.InvitedByUserId, cancellationToken);
        if (inviter is null || !inviter.IsActive)
        {
            throw new WorkspaceAccessDeniedException("The current account can no longer manage workspace invites.");
        }

        var normalizedEmail = NormalizeEmail(command.Email);
        var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(normalizedEmail.ToUpperInvariant(), cancellationToken);
        if (existingUser is not null)
        {
            var existingMembership = await _workspaceMembershipRepository.GetAsync(
                command.WorkspaceId,
                existingUser.Id,
                cancellationToken);

            if (existingMembership?.IsActive == true)
            {
                throw new WorkspaceInviteConflictException("That person is already a member of this workspace.");
            }
        }

        var utcNow = _clock.UtcNow;
        var existingInvitation = await _workspaceInvitationRepository.GetPendingForWorkspaceEmailAsync(
            command.WorkspaceId,
            normalizedEmail.ToUpperInvariant(),
            cancellationToken);

        if (existingInvitation is not null)
        {
            if (existingInvitation.CanBeAccepted(utcNow))
            {
                existingInvitation.Revoke(utcNow);
            }
            else
            {
                existingInvitation.Expire(utcNow);
            }

            await _workspaceInvitationRepository.UpdateAsync(existingInvitation, cancellationToken);
        }

        var rawToken = GenerateToken();
        var invitation = new WorkspaceInvitation(
            Guid.NewGuid(),
            workspace.Id,
            normalizedEmail,
            command.Role,
            HashToken(rawToken),
            command.InvitedByUserId,
            utcNow,
            utcNow.Add(InvitationLifetime));

        await _workspaceInvitationRepository.CreateAsync(invitation, cancellationToken);
        await _workspaceInvitationNotifier.SendInviteLinkAsync(
            new WorkspaceInvitationNotification(
                invitation.Email,
                workspace.Name,
                inviter.FullName,
                invitation.Role,
                rawToken,
                invitation.ExpiresAtUtc),
            cancellationToken);

        return new WorkspaceInvitationResultDto(
            InviteSuccessMessage,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAtUtc);
    }

    public async Task<AuthSessionDto> AcceptInviteAsync(
        AcceptWorkspaceInviteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Token))
        {
            throw new ArgumentException("Invite token is required.", nameof(command.Token));
        }

        var invitation = await _workspaceInvitationRepository.GetByTokenHashAsync(
            HashToken(command.Token),
            cancellationToken);

        var utcNow = _clock.UtcNow;
        if (invitation is null)
        {
            throw new InvalidWorkspaceInvitationException(InvalidInvitationMessage);
        }

        if (!invitation.CanBeAccepted(utcNow))
        {
            if (invitation.Status == WorkspaceInvitationStatus.Pending)
            {
                invitation.Expire(utcNow);
                await _workspaceInvitationRepository.UpdateAsync(invitation, cancellationToken);
            }

            throw new InvalidWorkspaceInvitationException(InvalidInvitationMessage);
        }

        var user = await _authRepository.GetUserByIdAsync(command.CurrentUserId, cancellationToken);
        if (user is null || !user.IsActive)
        {
            throw new InvalidWorkspaceInvitationException(InvalidInvitationMessage);
        }

        if (!string.Equals(user.NormalizedEmail, invitation.NormalizedEmail, StringComparison.Ordinal))
        {
            throw new WorkspaceInviteConflictException("This invite is for a different email address. Sign in with the invited account to continue.");
        }

        var workspace = await _workspaceRepository.GetByIdAsync(invitation.WorkspaceId, cancellationToken);
        if (workspace is null || !workspace.IsActive)
        {
            throw new InvalidWorkspaceInvitationException(InvalidInvitationMessage);
        }

        var existingMembership = await _workspaceMembershipRepository.GetAsync(
            invitation.WorkspaceId,
            user.Id,
            cancellationToken);

        if (existingMembership is null)
        {
            existingMembership = new WorkspaceMembership(
                Guid.NewGuid(),
                invitation.WorkspaceId,
                user.Id,
                invitation.Role,
                utcNow);

            await _workspaceMembershipRepository.AddAsync(existingMembership, cancellationToken);
        }
        else if (!existingMembership.IsActive)
        {
            existingMembership.ChangeRole(invitation.Role);
            existingMembership.Activate();
            await _workspaceMembershipRepository.UpdateAsync(existingMembership, cancellationToken);
        }

        invitation.Accept(utcNow);
        await _workspaceInvitationRepository.UpdateAsync(invitation, cancellationToken);

        var session = await _authRepository.GetSessionAsync(user.Id, invitation.WorkspaceId, cancellationToken);
        if (session is null)
        {
            throw new InvalidOperationException("The workspace membership could not be loaded after accepting the invite.");
        }

        return session;
    }

    public async Task<IReadOnlyList<WorkspaceMemberDto>> ListMembersAsync(
        ListWorkspaceMembersQuery query,
        CancellationToken cancellationToken = default)
    {
        await RequireWorkspaceAccessAsync(query.WorkspaceId, query.CurrentUserId, cancellationToken);

        var members = await _workspaceMembershipRepository.ListByWorkspaceAsync(query.WorkspaceId, cancellationToken);
        return members
            .Where(member => member.IsActive)
            .Select(member => new WorkspaceMemberDto(
                member.UserId,
                member.FullName,
                member.Email,
                member.Role,
                member.CreatedAtUtc,
                member.UserId == query.CurrentUserId))
            .ToArray();
    }

    private async Task<WorkspaceMembership> RequireWorkspaceAccessAsync(
        Guid workspaceId,
        Guid currentUserId,
        CancellationToken cancellationToken)
    {
        var membership = await _workspaceMembershipRepository.GetAsync(workspaceId, currentUserId, cancellationToken);
        if (membership is null || !membership.IsActive)
        {
            throw new WorkspaceAccessDeniedException("You do not have access to this workspace.");
        }

        return membership;
    }

    private static string NormalizeEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        try
        {
            return new MailAddress(email.Trim()).Address;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Email is invalid.", nameof(email), exception);
        }
    }

    private static string GenerateToken()
    {
        var tokenBytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToBase64String(tokenBytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
    }

    private static string HashToken(string token)
    {
        var normalizedToken = token.Trim();
        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(normalizedToken));
        return Convert.ToHexString(hashBytes);
    }
}
