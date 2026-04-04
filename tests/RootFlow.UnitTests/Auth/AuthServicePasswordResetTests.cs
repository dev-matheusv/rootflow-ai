using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Auth;
using RootFlow.Application.Auth.Commands;
using RootFlow.Application.Auth.Dtos;
using RootFlow.Domain.Users;
using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Auth;

public sealed class AuthServicePasswordResetTests
{
    [Fact]
    public async Task RequestPasswordResetAsync_ReturnsNeutralMessage_WhenUserDoesNotExist()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeAuthRepository();
        var notifier = new FakePasswordResetNotifier();
        var service = CreateService(repository, notifier, clock);

        var response = await service.RequestPasswordResetAsync(new ForgotPasswordCommand("missing@rootflow.test"));

        Assert.Equal("If the account exists, a reset link has been sent.", response);
        Assert.Null(repository.StoredPasswordResetToken);
        Assert.Empty(notifier.Notifications);
    }

    [Fact]
    public async Task RequestPasswordResetAsync_StoresHashedToken_AndSendsNotification()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeAuthRepository();
        var notifier = new FakePasswordResetNotifier();
        var user = repository.AddUser("jordan@rootflow.test", "Jordan Rivera", "HASH::CurrentPassword123!");
        var service = CreateService(repository, notifier, clock);

        var response = await service.RequestPasswordResetAsync(new ForgotPasswordCommand("jordan@rootflow.test"));

        Assert.Equal("If the account exists, a reset link has been sent.", response);
        Assert.NotNull(repository.StoredPasswordResetToken);
        Assert.Equal(user.Id, repository.StoredPasswordResetToken!.UserId);
        Assert.Equal(clock.UtcNow.AddHours(1), repository.StoredPasswordResetToken.ExpiresAtUtc);
        Assert.Single(notifier.Notifications);
        Assert.NotEqual(notifier.Notifications[0].Token, repository.StoredPasswordResetToken.TokenHash);
        Assert.Equal(64, repository.StoredPasswordResetToken.TokenHash.Length);
    }

    [Fact]
    public async Task ResetPasswordAsync_UpdatesPassword_AndPreventsTokenReuse()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeAuthRepository();
        var notifier = new FakePasswordResetNotifier();
        var user = repository.AddUser("jordan@rootflow.test", "Jordan Rivera", "HASH::CurrentPassword123!");
        var service = CreateService(repository, notifier, clock);

        await service.RequestPasswordResetAsync(new ForgotPasswordCommand(user.Email));
        var token = notifier.Notifications.Single().Token;

        await service.ResetPasswordAsync(new ResetPasswordCommand(token, "NewPassword123!"));

        Assert.Equal("HASH::NewPassword123!", repository.UsersById[user.Id].PasswordHash);
        Assert.Equal("HASH::NewPassword123!", repository.CompletedPasswordHash);
        Assert.NotNull(repository.StoredPasswordResetToken?.UsedAtUtc);

        var reuseException = await Assert.ThrowsAsync<InvalidPasswordResetTokenException>(() =>
            service.ResetPasswordAsync(new ResetPasswordCommand(token, "AnotherPassword123!")));

        Assert.Equal("This reset link is invalid or has expired. Request a new one.", reuseException.Message);
    }

    [Fact]
    public async Task ResetPasswordAsync_RejectsExpiredToken()
    {
        var clock = new FakeClock(new DateTime(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc));
        var repository = new FakeAuthRepository();
        var notifier = new FakePasswordResetNotifier();
        repository.AddUser("jordan@rootflow.test", "Jordan Rivera", "HASH::CurrentPassword123!");
        var service = CreateService(repository, notifier, clock);

        await service.RequestPasswordResetAsync(new ForgotPasswordCommand("jordan@rootflow.test"));
        var token = notifier.Notifications.Single().Token;
        clock.UtcNow = clock.UtcNow.AddHours(2);

        var exception = await Assert.ThrowsAsync<InvalidPasswordResetTokenException>(() =>
            service.ResetPasswordAsync(new ResetPasswordCommand(token, "NewPassword123!")));

        Assert.Equal("This reset link is invalid or has expired. Request a new one.", exception.Message);
    }

    private static AuthService CreateService(
        FakeAuthRepository repository,
        FakePasswordResetNotifier notifier,
        FakeClock clock)
    {
        return new AuthService(repository, new FakePasswordHashingService(), notifier, null, clock);
    }

    private sealed class FakeAuthRepository : IAuthRepository
    {
        public Dictionary<Guid, AppUser> UsersById { get; } = [];

        public PasswordResetToken? StoredPasswordResetToken { get; private set; }

        public string? CompletedPasswordHash { get; private set; }

        public AppUser AddUser(string email, string fullName, string passwordHash)
        {
            var user = new AppUser(Guid.NewGuid(), email, fullName, passwordHash, new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc));
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

        public Task CreateUserWorkspaceAsync(
            AppUser user,
            Workspace workspace,
            WorkspaceMembership membership,
            CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthSessionDto?> GetPrimarySessionAsync(Guid userId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<AuthSessionDto?> GetSessionAsync(Guid userId, Guid workspaceId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task StorePasswordResetTokenAsync(PasswordResetToken passwordResetToken, CancellationToken cancellationToken = default)
        {
            StoredPasswordResetToken = passwordResetToken;
            return Task.CompletedTask;
        }

        public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(string tokenHash, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<PasswordResetToken?>(StoredPasswordResetToken?.TokenHash == tokenHash ? StoredPasswordResetToken : null);
        }

        public Task CompletePasswordResetAsync(
            Guid userId,
            Guid passwordResetTokenId,
            string passwordHash,
            DateTime completedAtUtc,
            CancellationToken cancellationToken = default)
        {
            UsersById[userId].UpdatePasswordHash(passwordHash);
            CompletedPasswordHash = passwordHash;

            if (StoredPasswordResetToken?.Id == passwordResetTokenId)
            {
                StoredPasswordResetToken.MarkUsed(completedAtUtc);
            }

            return Task.CompletedTask;
        }
    }

    private sealed class FakePasswordHashingService : IPasswordHashingService
    {
        public string HashPassword(string password)
        {
            return $"HASH::{password}";
        }

        public bool VerifyPassword(string passwordHash, string providedPassword)
        {
            return passwordHash == $"HASH::{providedPassword}";
        }
    }

    private sealed class FakePasswordResetNotifier : IPasswordResetNotifier
    {
        public List<PasswordResetNotification> Notifications { get; } = [];

        public Task SendResetLinkAsync(PasswordResetNotification notification, CancellationToken cancellationToken = default)
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
}
