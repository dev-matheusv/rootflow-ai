namespace RootFlow.Domain.Users;

public sealed class PasswordResetToken
{
    private PasswordResetToken()
    {
    }

    public PasswordResetToken(
        Guid id,
        Guid userId,
        string tokenHash,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Password reset token id cannot be empty.", nameof(id));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("Password reset token user id cannot be empty.", nameof(userId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(tokenHash);

        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Password reset token expiry must be after creation time.", nameof(expiresAtUtc));
        }

        Id = id;
        UserId = userId;
        TokenHash = tokenHash.Trim();
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? UsedAtUtc { get; private set; }

    public bool IsActiveAt(DateTime utcNow)
    {
        return UsedAtUtc is null && utcNow <= ExpiresAtUtc;
    }

    public void MarkUsed(DateTime usedAtUtc)
    {
        if (UsedAtUtc.HasValue)
        {
            return;
        }

        if (usedAtUtc < CreatedAtUtc)
        {
            throw new InvalidOperationException("Password reset token usage cannot be before creation time.");
        }

        UsedAtUtc = usedAtUtc;
    }
}
