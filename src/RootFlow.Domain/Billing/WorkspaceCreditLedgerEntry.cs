namespace RootFlow.Domain.Billing;

public sealed class WorkspaceCreditLedgerEntry
{
    private WorkspaceCreditLedgerEntry()
    {
    }

    public WorkspaceCreditLedgerEntry(
        Guid id,
        Guid workspaceId,
        WorkspaceCreditLedgerType type,
        long amount,
        string description,
        DateTime createdAtUtc,
        string? referenceType = null,
        string? referenceId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Workspace credit ledger entry id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (amount == 0)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Ledger entry amount cannot be zero.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(description);
        ValidateAmountDirection(type, amount);

        Id = id;
        WorkspaceId = workspaceId;
        Type = type;
        Amount = amount;
        Description = description.Trim();
        ReferenceType = string.IsNullOrWhiteSpace(referenceType) ? null : referenceType.Trim();
        ReferenceId = string.IsNullOrWhiteSpace(referenceId) ? null : referenceId.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public WorkspaceCreditLedgerType Type { get; private set; }

    public long Amount { get; private set; }

    public string Description { get; private set; } = null!;

    public string? ReferenceType { get; private set; }

    public string? ReferenceId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    private static void ValidateAmountDirection(WorkspaceCreditLedgerType type, long amount)
    {
        switch (type)
        {
            case WorkspaceCreditLedgerType.SubscriptionGrant:
            case WorkspaceCreditLedgerType.CreditPurchase:
            case WorkspaceCreditLedgerType.Refund:
                if (amount < 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(amount), "Credit grant entries must use a positive amount.");
                }

                break;
            case WorkspaceCreditLedgerType.UsageDebit:
                if (amount > 0)
                {
                    throw new ArgumentOutOfRangeException(nameof(amount), "Usage debit entries must use a negative amount.");
                }

                break;
            case WorkspaceCreditLedgerType.ManualAdjustment:
                break;
            default:
                throw new ArgumentOutOfRangeException(nameof(type), type, "Unsupported workspace credit ledger type.");
        }
    }
}
