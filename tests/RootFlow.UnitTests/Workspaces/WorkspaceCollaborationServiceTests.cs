using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Application.Auth.Dtos;
using RootFlow.Application.Workspaces;
using RootFlow.Application.Workspaces.Commands;
using RootFlow.Application.Workspaces.Queries;
using RootFlow.Domain.Users;
using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Workspaces;

public sealed class WorkspaceCollaborationServiceTests
{
    [Fact]
    public async Task InviteAsync_StoresHashedToken_AndSendsNotification()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 14, 0, 0, DateTimeKind.Utc));
        var authRepository = new FakeAuthRepository();
        var workspaceRepository = new FakeWorkspaceRepository();
        var membershipRepository = new FakeWorkspaceMembershipRepository();
        var invitationRepository = new FakeWorkspaceInvitationRepository();
        var notifier = new FakeWorkspaceInvitationNotifier();

        var owner = authRepository.AddUser("owner@rootflow.test", "Owner User");
        var workspace = workspaceRepository.AddWorkspace("Acme Ops", "acme-ops", clock.UtcNow);
        membershipRepository.AddExisting(new WorkspaceMembership(
            Guid.NewGuid(),
            workspace.Id,
            owner.Id,
            WorkspaceRole.Owner,
            clock.UtcNow));

        var service = CreateService(
            authRepository,
            workspaceRepository,
            membershipRepository,
            invitationRepository,
            notifier,
            clock);

        var result = await service.InviteAsync(new InviteWorkspaceMemberCommand(
            workspace.Id,
            owner.Id,
            " teammate@rootflow.test ",
            WorkspaceRole.Admin));

        Assert.Equal("teammate@rootflow.test", result.Email);
        Assert.Equal(WorkspaceRole.Admin, result.Role);
        Assert.Equal(clock.UtcNow.AddDays(7), result.ExpiresAtUtc);
        Assert.NotNull(invitationRepository.StoredInvitation);
        Assert.Single(notifier.Notifications);
        Assert.Equal("teammate@rootflow.test", notifier.Notifications[0].Email);
        Assert.NotEqual(notifier.Notifications[0].Token, invitationRepository.StoredInvitation!.TokenHash);
        Assert.Equal(64, invitationRepository.StoredInvitation.TokenHash.Length);
    }

    [Fact]
    public async Task AcceptInviteAsync_CreatesMembership_AndReturnsWorkspaceSession()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 15, 0, 0, DateTimeKind.Utc));
        var authRepository = new FakeAuthRepository();
        var workspaceRepository = new FakeWorkspaceRepository();
        var membershipRepository = new FakeWorkspaceMembershipRepository();
        var invitationRepository = new FakeWorkspaceInvitationRepository();
        var notifier = new FakeWorkspaceInvitationNotifier();

        var invitee = authRepository.AddUser("invitee@rootflow.test", "Invitee User");
        var workspace = workspaceRepository.AddWorkspace("Acme Ops", "acme-ops", clock.UtcNow.AddDays(-2));
        var rawToken = "workspace-invite-token";
        invitationRepository.StoredInvitation = new WorkspaceInvitation(
            Guid.NewGuid(),
            workspace.Id,
            invitee.Email,
            WorkspaceRole.Member,
            HashToken(rawToken),
            Guid.NewGuid(),
            clock.UtcNow.AddMinutes(-10),
            clock.UtcNow.AddDays(7));

        var service = CreateService(
            authRepository,
            workspaceRepository,
            membershipRepository,
            invitationRepository,
            notifier,
            clock);

        var session = await service.AcceptInviteAsync(new AcceptWorkspaceInviteCommand(invitee.Id, rawToken));

        Assert.Equal(workspace.Id, session.Workspace.Id);
        Assert.Equal("Invitee User", session.User.FullName);
        Assert.Equal(WorkspaceRole.Member, session.Role);

        var membership = await membershipRepository.GetAsync(workspace.Id, invitee.Id);
        Assert.NotNull(membership);
        Assert.True(membership!.IsActive);
        Assert.Equal(WorkspaceRole.Member, membership.Role);
        Assert.Equal(WorkspaceInvitationStatus.Accepted, invitationRepository.StoredInvitation.Status);
    }

    [Fact]
    public async Task AcceptInviteAsync_RejectsWrongAccount()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 16, 0, 0, DateTimeKind.Utc));
        var authRepository = new FakeAuthRepository();
        var workspaceRepository = new FakeWorkspaceRepository();
        var membershipRepository = new FakeWorkspaceMembershipRepository();
        var invitationRepository = new FakeWorkspaceInvitationRepository();
        var notifier = new FakeWorkspaceInvitationNotifier();

        var signedInUser = authRepository.AddUser("other@rootflow.test", "Other User");
        var workspace = workspaceRepository.AddWorkspace("Acme Ops", "acme-ops", clock.UtcNow.AddDays(-2));
        var rawToken = "workspace-invite-token";
        invitationRepository.StoredInvitation = new WorkspaceInvitation(
            Guid.NewGuid(),
            workspace.Id,
            "invitee@rootflow.test",
            WorkspaceRole.Member,
            HashToken(rawToken),
            Guid.NewGuid(),
            clock.UtcNow.AddMinutes(-10),
            clock.UtcNow.AddDays(7));

        var service = CreateService(
            authRepository,
            workspaceRepository,
            membershipRepository,
            invitationRepository,
            notifier,
            clock);

        var exception = await Assert.ThrowsAsync<WorkspaceInviteConflictException>(() =>
            service.AcceptInviteAsync(new AcceptWorkspaceInviteCommand(signedInUser.Id, rawToken)));

        Assert.Equal("This invite is for a different email address. Sign in with the invited account to continue.", exception.Message);
    }

    [Fact]
    public async Task ListMembersAsync_ReturnsActiveMembersForWorkspace()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 17, 0, 0, DateTimeKind.Utc));
        var authRepository = new FakeAuthRepository();
        var workspaceRepository = new FakeWorkspaceRepository();
        var membershipRepository = new FakeWorkspaceMembershipRepository();
        var invitationRepository = new FakeWorkspaceInvitationRepository();
        var notifier = new FakeWorkspaceInvitationNotifier();

        var owner = authRepository.AddUser("owner@rootflow.test", "Owner User");
        var member = authRepository.AddUser("member@rootflow.test", "Member User");
        var workspace = workspaceRepository.AddWorkspace("Acme Ops", "acme-ops", clock.UtcNow.AddDays(-10));
        membershipRepository.AddExisting(new WorkspaceMembership(Guid.NewGuid(), workspace.Id, owner.Id, WorkspaceRole.Owner, clock.UtcNow.AddDays(-9)));
        membershipRepository.AddExisting(new WorkspaceMembership(Guid.NewGuid(), workspace.Id, member.Id, WorkspaceRole.Member, clock.UtcNow.AddDays(-7)));

        var service = CreateService(
            authRepository,
            workspaceRepository,
            membershipRepository,
            invitationRepository,
            notifier,
            clock);

        var members = await service.ListMembersAsync(new ListWorkspaceMembersQuery(workspace.Id, owner.Id));

        Assert.Equal(2, members.Count);
        Assert.Contains(members, candidate => candidate.UserId == owner.Id && candidate.IsCurrentUser);
        Assert.Contains(members, candidate => candidate.UserId == member.Id && !candidate.IsCurrentUser);
    }

    [Fact]
    public async Task InviteAsync_RejectsMembersWhoTryToInvite()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 18, 0, 0, DateTimeKind.Utc));
        var authRepository = new FakeAuthRepository();
        var workspaceRepository = new FakeWorkspaceRepository();
        var membershipRepository = new FakeWorkspaceMembershipRepository();
        var invitationRepository = new FakeWorkspaceInvitationRepository();
        var notifier = new FakeWorkspaceInvitationNotifier();

        var member = authRepository.AddUser("member@rootflow.test", "Member User");
        var workspace = workspaceRepository.AddWorkspace("Acme Ops", "acme-ops", clock.UtcNow);
        membershipRepository.AddExisting(new WorkspaceMembership(
            Guid.NewGuid(),
            workspace.Id,
            member.Id,
            WorkspaceRole.Member,
            clock.UtcNow));

        var service = CreateService(
            authRepository,
            workspaceRepository,
            membershipRepository,
            invitationRepository,
            notifier,
            clock);

        var exception = await Assert.ThrowsAsync<WorkspaceAccessDeniedException>(() =>
            service.InviteAsync(new InviteWorkspaceMemberCommand(
                workspace.Id,
                member.Id,
                "another@rootflow.test",
                WorkspaceRole.Member)));

        Assert.Equal("Only workspace owners and admins can send invites.", exception.Message);
        Assert.Null(invitationRepository.StoredInvitation);
        Assert.Empty(notifier.Notifications);
    }

    private static WorkspaceCollaborationService CreateService(
        FakeAuthRepository authRepository,
        FakeWorkspaceRepository workspaceRepository,
        FakeWorkspaceMembershipRepository membershipRepository,
        FakeWorkspaceInvitationRepository invitationRepository,
        FakeWorkspaceInvitationNotifier notifier,
        FakeClock clock)
    {
        return new WorkspaceCollaborationService(
            authRepository,
            workspaceRepository,
            membershipRepository,
            invitationRepository,
            notifier,
            new FakePasswordHashingService(),
            null,
            clock);
    }

    private static string HashToken(string token)
    {
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(hashBytes);
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public Dictionary<Guid, AppUser> UsersById { get; } = [];

        public AppUser AddUser(string email, string fullName)
        {
            var user = new AppUser(Guid.NewGuid(), email, fullName, $"HASH::{fullName}", new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
            UsersById[user.Id] = user;
            return user;
        }

        public Task<AppUser?> GetUserByNormalizedEmailAsync(string normalizedEmail, CancellationToken cancellationToken = default)
        {
            var user = UsersById.Values.SingleOrDefault(candidate => candidate.NormalizedEmail == normalizedEmail);
            return Task.FromResult<AppUser?>(user);
        }

        public Task<AppUser?> GetUserByIdAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            UsersById.TryGetValue(userId, out var user);
            return Task.FromResult<AppUser?>(user);
        }

        public Task<bool> WorkspaceSlugExistsAsync(string slug, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task CreateUserAsync(AppUser user, CancellationToken cancellationToken = default)
            => throw new NotSupportedException();

        public Task CreateUserWorkspaceAsync(AppUser user, Workspace workspace, WorkspaceMembership membership, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthSessionDto?> GetPrimarySessionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthSessionDto?> GetSessionAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
        {
            if (!UsersById.TryGetValue(userId, out var user))
            {
                return Task.FromResult<AuthSessionDto?>(null);
            }

            return Task.FromResult<AuthSessionDto?>(new AuthSessionDto(
                new AuthUserDto(user.Id, user.FullName, user.Email),
                new AuthWorkspaceDto(workspaceId, "Acme Ops", "acme-ops"),
                WorkspaceRole.Member));
        }

        public Task StorePasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task CompletePasswordResetAsync(Guid userId, Guid passwordResetTokenId, string passwordHash, DateTime completedAtUtc, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }
    }

    private sealed class FakeWorkspaceRepository : IWorkspaceRepository
    {
        public Dictionary<Guid, Workspace> Workspaces { get; } = [];

        public Workspace AddWorkspace(string name, string slug, DateTime createdAtUtc)
        {
            var workspace = new Workspace(Guid.NewGuid(), name, slug, createdAtUtc);
            Workspaces[workspace.Id] = workspace;
            return workspace;
        }

        public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
        {
            Workspaces[workspace.Id] = workspace;
            return Task.CompletedTask;
        }

        public Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(Workspaces.TryGetValue(workspaceId, out var workspace) && workspace.IsActive);
        }

        public Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            Workspaces.TryGetValue(workspaceId, out var workspace);
            return Task.FromResult<Workspace?>(workspace);
        }
    }

    private sealed class FakeWorkspaceMembershipRepository : IWorkspaceMembershipRepository
    {
        private readonly List<WorkspaceMembership> _memberships = [];

        public void AddExisting(WorkspaceMembership membership)
        {
            _memberships.Add(membership);
        }

        public Task<WorkspaceMembership?> GetAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
        {
            var membership = _memberships.SingleOrDefault(candidate => candidate.WorkspaceId == workspaceId && candidate.UserId == userId);
            return Task.FromResult<WorkspaceMembership?>(membership);
        }

        public Task AddAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default)
        {
            _memberships.Add(membership);
            return Task.CompletedTask;
        }

        public Task UpdateAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default)
        {
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkspaceMemberRecord>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            var members = _memberships
                .Where(candidate => candidate.WorkspaceId == workspaceId)
                .Select(candidate => new WorkspaceMemberRecord(
                    candidate.UserId,
                    candidate.UserId == _memberships[0].UserId ? "Owner User" : "Member User",
                    candidate.UserId == _memberships[0].UserId ? "owner@rootflow.test" : "member@rootflow.test",
                    candidate.Role,
                    candidate.CreatedAtUtc,
                    candidate.IsActive))
                .ToArray();

            return Task.FromResult<IReadOnlyList<WorkspaceMemberRecord>>(members);
        }
    }

    private sealed class FakeWorkspaceInvitationRepository : IWorkspaceInvitationRepository
    {
        public WorkspaceInvitation? StoredInvitation { get; set; }

        public Task CreateAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken = default)
        {
            StoredInvitation = invitation;
            return Task.CompletedTask;
        }

        public Task<WorkspaceInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceInvitation?>(StoredInvitation?.TokenHash == tokenHash ? StoredInvitation : null);
        }

        public Task<WorkspaceInvitation?> GetPendingForWorkspaceEmailAsync(Guid workspaceId, string normalizedEmail, CancellationToken cancellationToken = default)
        {
            var invitation = StoredInvitation is not null
                             && StoredInvitation.WorkspaceId == workspaceId
                             && StoredInvitation.NormalizedEmail == normalizedEmail
                             && StoredInvitation.Status == WorkspaceInvitationStatus.Pending
                ? StoredInvitation
                : null;

            return Task.FromResult<WorkspaceInvitation?>(invitation);
        }

        public Task UpdateAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken = default)
        {
            StoredInvitation = invitation;
            return Task.CompletedTask;
        }
    }

    private sealed class FakeWorkspaceInvitationNotifier : IWorkspaceInvitationNotifier
    {
        public List<WorkspaceInvitationNotification> Notifications { get; } = [];

        public Task SendInviteLinkAsync(WorkspaceInvitationNotification notification, CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FakeClock : IClock
    {
        public FakeClock(DateTime utcNow)
        {
            UtcNow = utcNow;
        }

        public DateTime UtcNow { get; set; }
    }

    private sealed class FakePasswordHashingService : IPasswordHashingService
    {
        public string HashPassword(string password) => $"HASH::{password}";
        public bool VerifyPassword(string passwordHash, string providedPassword) => passwordHash == $"HASH::{providedPassword}";
    }
}
