using RootFlow.Domain.Users;

namespace RootFlow.UnitTests.Domain;

public sealed class PasswordResetTokenTests
{
    [Fact]
    public void Constructor_StartsActiveUntilExpiry()
    {
        var createdAtUtc = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = createdAtUtc.AddHours(1);

        var token = new PasswordResetToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "TOKEN_HASH",
            createdAtUtc,
            expiresAtUtc);

        Assert.True(token.IsActiveAt(createdAtUtc.AddMinutes(30)));
        Assert.True(token.IsActiveAt(expiresAtUtc));
    }

    [Fact]
    public void MarkUsed_DeactivatesToken()
    {
        var createdAtUtc = new DateTime(2026, 4, 1, 12, 0, 0, DateTimeKind.Utc);
        var usedAtUtc = createdAtUtc.AddMinutes(15);

        var token = new PasswordResetToken(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "TOKEN_HASH",
            createdAtUtc,
            createdAtUtc.AddHours(1));

        token.MarkUsed(usedAtUtc);

        Assert.Equal(usedAtUtc, token.UsedAtUtc);
        Assert.False(token.IsActiveAt(createdAtUtc.AddMinutes(16)));
    }
}
