namespace RootFlow.Api.Contracts.Billing;

public sealed record WorkspaceBillingSummaryResponse(
    string? CurrentPlanName,
    string? SubscriptionStatus,
    DateTime? TrialEndsAtUtc,
    bool IsDegraded,
    BillingPlanResponse? BillingPlan,
    WorkspaceSubscriptionResponse? Subscription,
    WorkspaceCreditBalanceResponse Balance);
