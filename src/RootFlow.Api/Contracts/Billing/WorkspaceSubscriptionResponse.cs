namespace RootFlow.Api.Contracts.Billing;

public sealed record WorkspaceSubscriptionResponse(
    Guid Id,
    Guid WorkspaceId,
    Guid BillingPlanId,
    string Status,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    DateTime? TrialEndsAtUtc,
    DateTime? CanceledAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
