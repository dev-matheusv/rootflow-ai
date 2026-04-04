namespace RootFlow.Domain.Billing;

public sealed class WorkspaceSubscription
{
    private WorkspaceSubscription()
    {
    }

    public WorkspaceSubscription(
        Guid id,
        Guid workspaceId,
        Guid billingPlanId,
        WorkspaceSubscriptionStatus status,
        DateTime currentPeriodStartUtc,
        DateTime currentPeriodEndUtc,
        DateTime createdAtUtc,
        DateTime updatedAtUtc,
        DateTime? canceledAtUtc = null,
        DateTime? trialEndsAtUtc = null,
        string? provider = null,
        string? providerCustomerId = null,
        string? providerSubscriptionId = null,
        string? providerPriceId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Workspace subscription id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (billingPlanId == Guid.Empty)
        {
            throw new ArgumentException("Billing plan id cannot be empty.", nameof(billingPlanId));
        }

        if (currentPeriodEndUtc <= currentPeriodStartUtc)
        {
            throw new ArgumentException("Current billing period must end after it starts.", nameof(currentPeriodEndUtc));
        }

        if (status == WorkspaceSubscriptionStatus.Trial && trialEndsAtUtc is null)
        {
            throw new ArgumentException("Trial subscriptions must include a trial end timestamp.", nameof(trialEndsAtUtc));
        }

        if (trialEndsAtUtc.HasValue && trialEndsAtUtc.Value <= currentPeriodStartUtc)
        {
            throw new ArgumentException("Trial end must be after the current period start.", nameof(trialEndsAtUtc));
        }

        Id = id;
        WorkspaceId = workspaceId;
        BillingPlanId = billingPlanId;
        Status = status;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        CanceledAtUtc = canceledAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        TrialEndsAtUtc = trialEndsAtUtc;
        Provider = NormalizeOptional(provider);
        ProviderCustomerId = NormalizeOptional(providerCustomerId);
        ProviderSubscriptionId = NormalizeOptional(providerSubscriptionId);
        ProviderPriceId = NormalizeOptional(providerPriceId);
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid BillingPlanId { get; private set; }

    public WorkspaceSubscriptionStatus Status { get; private set; }

    public DateTime CurrentPeriodStartUtc { get; private set; }

    public DateTime CurrentPeriodEndUtc { get; private set; }

    public DateTime? CanceledAtUtc { get; private set; }

    public DateTime? TrialEndsAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public string? Provider { get; private set; }

    public string? ProviderCustomerId { get; private set; }

    public string? ProviderSubscriptionId { get; private set; }

    public string? ProviderPriceId { get; private set; }

    public bool IsActiveAt(DateTime asOfUtc)
    {
        if (CanceledAtUtc is not null || CurrentPeriodStartUtc > asOfUtc)
        {
            return false;
        }

        return Status switch
        {
            WorkspaceSubscriptionStatus.Active => CurrentPeriodEndUtc > asOfUtc,
            WorkspaceSubscriptionStatus.Trial => TrialEndsAtUtc.HasValue && TrialEndsAtUtc.Value > asOfUtc,
            _ => false
        };
    }

    public bool ShouldExpireAt(DateTime asOfUtc)
    {
        if (CanceledAtUtc is not null)
        {
            return false;
        }

        return Status switch
        {
            WorkspaceSubscriptionStatus.Active => CurrentPeriodEndUtc <= asOfUtc,
            WorkspaceSubscriptionStatus.Trial => TrialEndsAtUtc.HasValue && TrialEndsAtUtc.Value <= asOfUtc,
            _ => false
        };
    }

    public void ChangePlan(
        Guid billingPlanId,
        DateTime currentPeriodStartUtc,
        DateTime currentPeriodEndUtc,
        DateTime updatedAtUtc,
        string? provider = null,
        string? providerCustomerId = null,
        string? providerSubscriptionId = null,
        string? providerPriceId = null)
    {
        if (billingPlanId == Guid.Empty)
        {
            throw new ArgumentException("Billing plan id cannot be empty.", nameof(billingPlanId));
        }

        if (currentPeriodEndUtc <= currentPeriodStartUtc)
        {
            throw new ArgumentException("Current billing period must end after it starts.", nameof(currentPeriodEndUtc));
        }

        BillingPlanId = billingPlanId;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        Status = WorkspaceSubscriptionStatus.Active;
        CanceledAtUtc = null;
        TrialEndsAtUtc = null;
        UpdatedAtUtc = updatedAtUtc;
        Provider = NormalizeOptional(provider);
        ProviderCustomerId = NormalizeOptional(providerCustomerId);
        ProviderSubscriptionId = NormalizeOptional(providerSubscriptionId);
        ProviderPriceId = NormalizeOptional(providerPriceId);
    }

    public void Cancel(DateTime canceledAtUtc)
    {
        Status = WorkspaceSubscriptionStatus.Canceled;
        CanceledAtUtc = canceledAtUtc;
        UpdatedAtUtc = canceledAtUtc;
    }

    public void MarkExpired(DateTime expiredAtUtc)
    {
        Status = WorkspaceSubscriptionStatus.Expired;
        UpdatedAtUtc = expiredAtUtc;
    }

    public void SyncProviderSubscription(
        Guid billingPlanId,
        WorkspaceSubscriptionStatus status,
        DateTime currentPeriodStartUtc,
        DateTime currentPeriodEndUtc,
        DateTime updatedAtUtc,
        string provider,
        string? providerCustomerId,
        string providerSubscriptionId,
        string? providerPriceId,
        DateTime? canceledAtUtc = null)
    {
        if (billingPlanId == Guid.Empty)
        {
            throw new ArgumentException("Billing plan id cannot be empty.", nameof(billingPlanId));
        }

        if (currentPeriodEndUtc <= currentPeriodStartUtc)
        {
            throw new ArgumentException("Current billing period must end after it starts.", nameof(currentPeriodEndUtc));
        }

        if (status == WorkspaceSubscriptionStatus.Trial)
        {
            throw new ArgumentException("Provider-managed subscriptions cannot be synced as trial subscriptions.", nameof(status));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerSubscriptionId);

        BillingPlanId = billingPlanId;
        Status = status;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        TrialEndsAtUtc = null;
        CanceledAtUtc = status == WorkspaceSubscriptionStatus.Canceled
            ? canceledAtUtc ?? updatedAtUtc
            : null;
        UpdatedAtUtc = updatedAtUtc;
        Provider = provider.Trim().ToLowerInvariant();
        ProviderCustomerId = NormalizeOptional(providerCustomerId);
        ProviderSubscriptionId = providerSubscriptionId.Trim();
        ProviderPriceId = NormalizeOptional(providerPriceId);
    }

    private static string? NormalizeOptional(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? null
            : value.Trim();
    }
}
