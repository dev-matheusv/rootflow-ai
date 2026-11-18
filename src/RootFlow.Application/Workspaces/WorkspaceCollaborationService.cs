using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Application.Auth.Dtos;
using RootFlow.Application.Workspaces.Commands;
using RootFlow.Application.Workspaces.Dtos;
using RootFlow.Application.Workspaces.Queries;
using RootFlow.Domain.Users;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Workspaces;

public sealed class WorkspaceCollaborationService
{
    private static readonly TimeSpan InvitationLifetime = TimeSpan.FromDays(7);
    private const string InviteSuccessMessage = "Invite sent. If the address can access this workspace, they can use the link to join.";
    private const string InviteNotDeliveredMessage = "The invite was created, but e-mail delivery is not configured yet, so the person was not notified.";
    private const string InvalidInvitationMessage = "This workspace invite is invalid or has expired.";
    private readonly IAuthRepository _authRepository;
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;
    private readonly IWorkspaceInvitationRepository _workspaceInvitationRepository;
    private readonly IWorkspaceInvitationNotifier _workspaceInvitationNotifier;
    private readonly IPasswordHashingService _passwordHashingService;
    private readonly IPlatformAdminAccessService? _platformAdminAccessService;
    private readonly IClock _clock;

    public WorkspaceCollaborationService(
        IAuthRepository authRepository,
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository,
        IWorkspaceInvitationRepository workspaceInvitationRepository,
        IWorkspaceInvitationNotifier workspaceInvitationNotifier,
        IPasswordHashingService passwordHashingService,
        IPlatformAdminAccessService? platformAdminAccessService,
        IClock clock)
    {
        _authRepository = authRepository;
        _workspaceRepository = workspaceRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
        _workspaceInvitationRepository = workspaceInvitationRepository;
        _workspaceInvitationNotifier = workspaceInvitationNotifier;
        _passwordHashingService = passwordHashingService;
        _platformAdminAccessService = platformAdminAccessService;
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

        // RF-11: an invite never grants more power than the inviter has. Owner is never granted by invite
        // (ownership transfer is a separate flow), and Admins can only invite Members.
        if (command.Role == WorkspaceRole.Owner)
        {
            throw new WorkspaceAccessDeniedException("The owner role cannot be granted by invite.");
        }

        if (inviterMembership.Role == WorkspaceRole.Admin && command.Role != WorkspaceRole.Member)
        {
            throw new WorkspaceAccessDeniedException("Admins can only invite members.");
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

        var delivered = _workspaceInvitationNotifier.CanDeliver;
        return new WorkspaceInvitationResultDto(
            delivered ? InviteSuccessMessage : InviteNotDeliveredMessage,
            invitation.Email,
            invitation.Role,
            invitation.ExpiresAtUtc,
            delivered);
    }

    /// <summary>Upper bound per request: protects the e-mail provider and keeps one call short.</summary>
    public const int MaxBulkInvites = 200;

    /// <summary>
    /// RF-14: invites a list of people with the same role. Each address is handled on its own: one bad
    /// address never blocks the others, and the result says what happened to each. Permission rules are
    /// the same as a single invite (checked once up front, then again by InviteAsync).
    /// </summary>
    public async Task<BulkInviteResultDto> InviteManyAsync(
        InviteWorkspaceMembersBulkCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Emails.Count == 0)
        {
            throw new ArgumentException("At least one email is required.", nameof(command.Emails));
        }

        if (command.Emails.Count > MaxBulkInvites)
        {
            throw new ArgumentException($"At most {MaxBulkInvites} emails per request.", nameof(command.Emails));
        }

        // Fail fast on permission problems instead of returning N identical errors.
        var inviterMembership = await RequireWorkspaceAccessAsync(command.WorkspaceId, command.InvitedByUserId, cancellationToken);
        if (inviterMembership.Role is not (WorkspaceRole.Owner or WorkspaceRole.Admin))
        {
            throw new WorkspaceAccessDeniedException("Only workspace owners and admins can send invites.");
        }

        var items = new List<BulkInviteItemDto>(command.Emails.Count);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var sent = 0;

        foreach (var rawEmail in command.Emails)
        {
            var email = rawEmail?.Trim() ?? string.Empty;
            if (email.Length == 0)
            {
                continue;
            }

            if (!seen.Add(email))
            {
                items.Add(new BulkInviteItemDto(email, "duplicate", null));
                continue;
            }

            try
            {
                var result = await InviteAsync(
                    new InviteWorkspaceMemberCommand(command.WorkspaceId, command.InvitedByUserId, email, command.Role),
                    cancellationToken);
                if (result.EmailDelivered)
                {
                    items.Add(new BulkInviteItemDto(email, "sent", null));
                    sent++;
                }
                else
                {
                    items.Add(new BulkInviteItemDto(email, "not_delivered", result.Message));
                }
            }
            catch (ArgumentException)
            {
                items.Add(new BulkInviteItemDto(email, "invalid_email", "Email is invalid."));
            }
            catch (WorkspaceInviteConflictException exception)
            {
                items.Add(new BulkInviteItemDto(email, "already_member", exception.Message));
            }
            catch (WorkspaceAccessDeniedException)
            {
                throw;
            }
            catch (Exception exception) when (exception is not OperationCanceledException)
            {
                // Delivery problems for one address (e.g. provider error) must not stop the batch.
                items.Add(new BulkInviteItemDto(email, "failed", "The invite could not be sent. Try again later."));
            }
        }

        return new BulkInviteResultDto(sent, items.Count - sent, items);
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

        return session with
        {
            IsPlatformAdmin = _platformAdminAccessService?.HasAccess(session.User.Email) == true
        };
    }

    public async Task<InviteLookupDto> LookupInviteAsync(
        LookupInviteQuery query,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(query.Token))
            return new InviteLookupDto("", "", "", false, false, "Invalid invite link.");

        var invitation = await _workspaceInvitationRepository.GetByTokenHashAsync(
            HashToken(query.Token), cancellationToken);

        var utcNow = _clock.UtcNow;
        if (invitation is null || !invitation.CanBeAccepted(utcNow))
            return new InviteLookupDto("", "", "", false, false, "This invite is invalid or has expired.");

        var workspace = await _workspaceRepository.GetByIdAsync(invitation.WorkspaceId, cancellationToken);
        if (workspace is null || !workspace.IsActive)
            return new InviteLookupDto("", "", "", false, false, "This invite is invalid or has expired.");

        var inviter = await _authRepository.GetUserByIdAsync(invitation.InvitedByUserId, cancellationToken);
        var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(
            invitation.NormalizedEmail, cancellationToken);

        return new InviteLookupDto(
            invitation.Email,
            workspace.Name,
            inviter?.FullName ?? "",
            existingUser is not null,
            true);
    }

    public async Task<AuthSessionDto> SignupAndAcceptInviteAsync(
        SignupAndAcceptInviteCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.InviteToken))
            throw new InvalidWorkspaceInvitationException(InvalidInvitationMessage);

        var invitation = await _workspaceInvitationRepository.GetByTokenHashAsync(
            HashToken(command.InviteToken), cancellationToken);

        var utcNow = _clock.UtcNow;
        if (invitation is null || !invitation.CanBeAccepted(utcNow))
            throw new InvalidWorkspaceInvitationException(InvalidInvitationMessage);

        var workspace = await _workspaceRepository.GetByIdAsync(invitation.WorkspaceId, cancellationToken);
        if (workspace is null || !workspace.IsActive)
            throw new InvalidWorkspaceInvitationException(InvalidInvitationMessage);

        var existingUser = await _authRepository.GetUserByNormalizedEmailAsync(
            invitation.NormalizedEmail, cancellationToken);
        if (existingUser is not null)
            throw new WorkspaceInviteConflictException("An account with this email already exists. Please sign in to accept the invite.");

        var fullName = command.FullName?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(fullName) || fullName.Length > 120)
            throw new ArgumentException("Full name is required and must be 120 characters or less.", nameof(command.FullName));

        if (string.IsNullOrWhiteSpace(command.Password) || command.Password.Length < 8 || command.Password.Length > 128)
            throw new ArgumentException("Password must be between 8 and 128 characters.", nameof(command.Password));

        var user = new AppUser(
            Guid.NewGuid(),
            invitation.Email,
            fullName,
            _passwordHashingService.HashPassword(command.Password),
            utcNow);

        await _authRepository.CreateUserAsync(user, cancellationToken);

        var membership = new WorkspaceMembership(
            Guid.NewGuid(),
            invitation.WorkspaceId,
            user.Id,
            invitation.Role,
            utcNow);

        await _workspaceMembershipRepository.AddAsync(membership, cancellationToken);

        invitation.Accept(utcNow);
        await _workspaceInvitationRepository.UpdateAsync(invitation, cancellationToken);

        var session = await _authRepository.GetSessionAsync(user.Id, invitation.WorkspaceId, cancellationToken);
        if (session is null)
            throw new InvalidOperationException("Session could not be loaded after accepting the invite.");

        return session with
        {
            IsPlatformAdmin = _platformAdminAccessService?.HasAccess(session.User.Email) == true
        };
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

    /// <summary>
    /// Changes the role of another member. Rules:
    /// only Owner and Admin manage members; nobody changes their own role;
    /// the Owner role is never granted or taken here (ownership transfer is a separate flow);
    /// an Admin manages Members only, an Owner manages Admins and Members.
    /// </summary>
    public async Task<WorkspaceMemberDto> ChangeMemberRoleAsync(
        ChangeWorkspaceMemberRoleCommand command,
        CancellationToken cancellationToken = default)
    {
        if (command.Role == WorkspaceRole.Owner)
        {
            throw new WorkspaceAccessDeniedException("The owner role cannot be assigned here.");
        }

        var target = await RequireManageableMemberAsync(
            command.WorkspaceId,
            command.ActorUserId,
            command.TargetUserId,
            cancellationToken);

        if (target.Role != command.Role)
        {
            target.ChangeRole(command.Role);
            await _workspaceMembershipRepository.UpdateAsync(target, cancellationToken);
        }

        var members = await _workspaceMembershipRepository.ListByWorkspaceAsync(command.WorkspaceId, cancellationToken);
        var record = members.Single(member => member.UserId == command.TargetUserId);

        return new WorkspaceMemberDto(
            record.UserId,
            record.FullName,
            record.Email,
            target.Role,
            record.CreatedAtUtc,
            false);
    }

    /// <summary>
    /// Removes another member from the workspace. The membership is deactivated, not deleted,
    /// so history (conversations, attempts, audit) keeps a valid reference. Access ends on the
    /// next request because the API revalidates the membership on every authenticated call.
    /// </summary>
    public async Task RemoveMemberAsync(
        RemoveWorkspaceMemberCommand command,
        CancellationToken cancellationToken = default)
    {
        var target = await RequireManageableMemberAsync(
            command.WorkspaceId,
            command.ActorUserId,
            command.TargetUserId,
            cancellationToken);

        target.Deactivate();
        await _workspaceMembershipRepository.UpdateAsync(target, cancellationToken);
    }

    private async Task<WorkspaceMembership> RequireManageableMemberAsync(
        Guid workspaceId,
        Guid actorUserId,
        Guid targetUserId,
        CancellationToken cancellationToken)
    {
        var actor = await RequireWorkspaceAccessAsync(workspaceId, actorUserId, cancellationToken);
        if (actor.Role is not (WorkspaceRole.Owner or WorkspaceRole.Admin))
        {
            throw new WorkspaceAccessDeniedException("Only workspace owners and admins can manage members.");
        }

        if (actorUserId == targetUserId)
        {
            throw new WorkspaceAccessDeniedException("You cannot change your own membership.");
        }

        var target = await _workspaceMembershipRepository.GetAsync(workspaceId, targetUserId, cancellationToken);
        if (target is null || !target.IsActive)
        {
            throw new WorkspaceMemberNotFoundException("That person is not a member of this workspace.");
        }

        if (target.Role == WorkspaceRole.Owner)
        {
            throw new WorkspaceAccessDeniedException("The workspace owner cannot be changed or removed.");
        }

        if (actor.Role == WorkspaceRole.Admin && target.Role != WorkspaceRole.Member)
        {
            throw new WorkspaceAccessDeniedException("Admins can only manage members.");
        }

        return target;
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
