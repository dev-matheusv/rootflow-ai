namespace RootFlow.Domain.Billing;

public sealed class WorkspaceBillingTransaction
{
    private WorkspaceBillingTransaction()
    {
    }

    public WorkspaceBillingTransaction(
        Guid id,
        Guid workspaceId,
        string provider,
        WorkspaceBillingTransactionType type,
        WorkspaceBillingTransactionStatus status,
        decimal amount,
        string currencyCode,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        Guid? billingPlanId = null,
        long? creditAmount = null,
        string? externalCheckoutSessionId = null,
        string? externalPaymentIntentId = null,
        string? externalSubscriptionId = null,
        string? externalInvoiceId = null,
        string? externalCustomerId = null,
        DateTime? completedAtUtc = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Workspace billing transaction id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (amount < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(amount), "Billing transaction amount cannot be negative.");
        }

        if (creditAmount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(creditAmount), "Credit amount cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(currencyCode);

        Id = id;
        WorkspaceId = workspaceId;
        Provider = provider.Trim().ToLowerInvariant();
        Type = type;
        Status = status;
        Amount = decimal.Round(amount, 2, MidpointRounding.AwayFromZero);
        CurrencyCode = currencyCode.Trim().ToUpperInvariant();
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        BillingPlanId = billingPlanId;
        CreditAmount = creditAmount;
        ExternalCheckoutSessionId = NormalizeOptional(externalCheckoutSessionId);
        ExternalPaymentIntentId = NormalizeOptional(externalPaymentIntentId);
        ExternalSubscriptionId = NormalizeOptional(externalSubscriptionId);
        ExternalInvoiceId = NormalizeOptional(externalInvoiceId);
        ExternalCustomerId = NormalizeOptional(externalCustomerId);
        CompletedAtUtc = completedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Provider { get; private set; } = null!;

    public WorkspaceBillingTransactionType Type { get; private set; }

    public WorkspaceBillingTransactionStatus Status { get; private set; }

    public Guid? BillingPlanId { get; private set; }

    public long? CreditAmount { get; private set; }

    public decimal Amount { get; private set; }

    public string CurrencyCode { get; private set; } = null!;

    public string? ExternalCheckoutSessionId { get; private set; }

    public string? ExternalPaymentIntentId { get; private set; }

    public string? ExternalSubscriptionId { get; private set; }

    public string? ExternalInvoiceId { get; private set; }

    public string? ExternalCustomerId { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? CompletedAtUtc { get; private set; }

    public bool IsCompleted =>
        Status == WorkspaceBillingTransactionStatus.Completed && CompletedAtUtc.HasValue;

    public void MarkCompleted(
        DateTime completedAtUtc,
        string? externalCheckoutSessionId = null,
        string? externalPaymentIntentId = null,
        string? externalSubscriptionId = null,
        string? externalInvoiceId = null,
        string? externalCustomerId = null)
    {
        Status = WorkspaceBillingTransactionStatus.Completed;
        UpdatedAtUtc = completedAtUtc;
        CompletedAtUtc = completedAtUtc;
        ExternalCheckoutSessionId = NormalizeOptional(externalCheckoutSessionId) ?? ExternalCheckoutSessionId;
        ExternalPaymentIntentId = NormalizeOptional(externalPaymentIntentId) ?? ExternalPaymentIntentId;
        ExternalSubscriptionId = NormalizeOptional(externalSubscriptionId) ?? ExternalSubscriptionId;
        ExternalInvoiceId = NormalizeOptional(externalInvoiceId) ?? ExternalInvoiceId;
        ExternalCustomerId = NormalizeOptional(externalCustomerId) ?? ExternalCustomerId;
    }

    public void MarkCanceled(DateTime canceledAtUtc)
    {
        Status = WorkspaceBillingTransactionStatus.Canceled;
        UpdatedAtUtc = canceledAtUtc;
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
