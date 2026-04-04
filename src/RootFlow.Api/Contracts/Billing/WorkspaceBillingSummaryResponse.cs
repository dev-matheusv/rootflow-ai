namespace RootFlow.Api.Contracts.Billing;

public sealed record WorkspaceBillingSummaryResponse(
    BillingPlanResponse? BillingPlan,
    WorkspaceSubscriptionResponse? Subscription,
    WorkspaceCreditBalanceResponse Balance);
