namespace RootFlow.Application.Billing.Dtos;

public sealed record WorkspaceCreditSummaryDto(
    BillingPlanDto? BillingPlan,
    WorkspaceSubscriptionDto? Subscription,
    WorkspaceCreditBalanceDto Balance,
    bool IsDegraded = false);
