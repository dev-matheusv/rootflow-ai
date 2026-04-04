using RootFlow.Api.Contracts.Billing;
using RootFlow.Application.Billing.Dtos;

namespace RootFlow.Api.Auth;

public static class BillingContractMapper
{
    public static BillingPlanResponse ToResponse(this BillingPlanDto plan)
    {
        return new BillingPlanResponse(
            plan.Id,
            plan.Code,
            plan.Name,
            plan.MonthlyPrice,
            plan.CurrencyCode,
            plan.IncludedCredits,
            plan.MaxUsers,
            plan.IsActive);
    }

    public static WorkspaceBillingSummaryResponse ToResponse(this WorkspaceCreditSummaryDto summary)
    {
        return new WorkspaceBillingSummaryResponse(
            summary.BillingPlan?.ToResponse(),
            summary.Subscription?.ToResponse(),
            summary.Balance.ToResponse());
    }

    public static WorkspaceSubscriptionResponse ToResponse(this WorkspaceSubscriptionDto subscription)
    {
        return new WorkspaceSubscriptionResponse(
            subscription.Id,
            subscription.WorkspaceId,
            subscription.BillingPlanId,
            subscription.Status.ToString(),
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.TrialEndsAtUtc,
            subscription.CanceledAtUtc,
            subscription.CreatedAtUtc,
            subscription.UpdatedAtUtc);
    }

    public static WorkspaceCreditBalanceResponse ToResponse(this WorkspaceCreditBalanceDto balance)
    {
        return new WorkspaceCreditBalanceResponse(
            balance.WorkspaceId,
            balance.AvailableCredits,
            balance.ConsumedCredits,
            balance.UpdatedAtUtc);
    }
}
