namespace RootFlow.Domain.Billing;

public sealed class WorkspaceCreditBalance
{
    private WorkspaceCreditBalance()
    {
    }

    public WorkspaceCreditBalance(
        Guid workspaceId,
        long availableCredits,
        long consumedCredits,
        DateTime updatedAtUtc)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (availableCredits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(availableCredits), "Available credits cannot be negative.");
        }

        if (consumedCredits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(consumedCredits), "Consumed credits cannot be negative.");
        }

        WorkspaceId = workspaceId;
        AvailableCredits = availableCredits;
        ConsumedCredits = consumedCredits;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid WorkspaceId { get; private set; }

    public long AvailableCredits { get; private set; }

    public long ConsumedCredits { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool HasAvailableCredits(long requiredCredits)
    {
        if (requiredCredits < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(requiredCredits), "Required credits cannot be negative.");
        }

        return AvailableCredits >= requiredCredits;
    }

    public void GrantCredits(long amount, DateTime updatedAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Granted credits must be greater than zero.");
        }

        AvailableCredits += amount;
        UpdatedAtUtc = updatedAtUtc;
    }

    public void ConsumeCredits(long amount, DateTime updatedAtUtc)
    {
        if (amount <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Consumed credits must be greater than zero.");
        }

        if (!HasAvailableCredits(amount))
        {
            throw new InvalidOperationException("Workspace does not have enough credits.");
        }

        AvailableCredits -= amount;
        ConsumedCredits += amount;
        UpdatedAtUtc = updatedAtUtc;
    }
}
