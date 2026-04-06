namespace RootFlow.Api.Contracts.Billing;

public sealed record WorkspaceBillingSummaryResponse(
    string? CurrentPlanName,
    string? SubscriptionStatus,
    DateTime? TrialEndsAtUtc,
    BillingPlanResponse? BillingPlan,
    WorkspaceSubscriptionResponse? Subscription,
    WorkspaceCreditBalanceResponse Balance);
