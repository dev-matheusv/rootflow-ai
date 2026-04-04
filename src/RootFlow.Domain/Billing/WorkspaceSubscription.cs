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
        DateTime? canceledAtUtc = null)
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

        Id = id;
        WorkspaceId = workspaceId;
        BillingPlanId = billingPlanId;
        Status = status;
        CurrentPeriodStartUtc = currentPeriodStartUtc;
        CurrentPeriodEndUtc = currentPeriodEndUtc;
        CanceledAtUtc = canceledAtUtc;
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = updatedAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid BillingPlanId { get; private set; }

    public WorkspaceSubscriptionStatus Status { get; private set; }

    public DateTime CurrentPeriodStartUtc { get; private set; }

    public DateTime CurrentPeriodEndUtc { get; private set; }

    public DateTime? CanceledAtUtc { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public bool IsActiveAt(DateTime asOfUtc)
    {
        return Status == WorkspaceSubscriptionStatus.Active
            && CanceledAtUtc is null
            && CurrentPeriodStartUtc <= asOfUtc
            && CurrentPeriodEndUtc > asOfUtc;
    }

    public void ChangePlan(
        Guid billingPlanId,
        DateTime currentPeriodStartUtc,
        DateTime currentPeriodEndUtc,
        DateTime updatedAtUtc)
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
        UpdatedAtUtc = updatedAtUtc;
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
}
