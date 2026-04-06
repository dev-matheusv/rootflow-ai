using RootFlow.Api.Contracts.Admin;
using RootFlow.Application.PlatformAdmin.Dtos;

namespace RootFlow.Api.Admin;

public static class PlatformAdminContractMapper
{
    public static PlatformAdminDashboardResponse ToResponse(this PlatformAdminDashboardDto dashboard)
    {
        return new PlatformAdminDashboardResponse(
            dashboard.Overview.ToResponse(),
            dashboard.Alerts.ToResponse(),
            dashboard.UsageWindows.Select(ToResponse).ToArray(),
            dashboard.LowCreditWorkspaces.Select(ToResponse).ToArray(),
            dashboard.NoCreditWorkspaces.Select(ToResponse).ToArray(),
            dashboard.TrialsExpiringSoon.Select(ToResponse).ToArray(),
            dashboard.PaymentIssues.Select(ToResponse).ToArray(),
            dashboard.StripeWebhookIssues.Select(ToResponse).ToArray(),
            dashboard.RecentCreditPurchases.Select(ToResponse).ToArray(),
            dashboard.RecentSubscriptionChanges.Select(ToResponse).ToArray(),
            dashboard.TopCreditConsumers.Select(ToResponse).ToArray(),
            dashboard.TopProviderCostWorkspaces.Select(ToResponse).ToArray(),
            dashboard.TopRevenueBasisWorkspaces.Select(ToResponse).ToArray(),
            dashboard.ModelBreakdown.Select(ToResponse).ToArray());
    }

    private static PlatformAdminOverviewResponse ToResponse(this PlatformAdminOverviewDto overview)
    {
        return new PlatformAdminOverviewResponse(
            overview.TotalWorkspaces,
            overview.TotalActiveSubscriptions,
            overview.TotalTrials,
            overview.TotalUsers,
            overview.TotalAvailableCredits,
            overview.TotalConsumedCredits,
            overview.EstimatedProviderCost,
            overview.EstimatedRevenueBasis,
            overview.EstimatedGrossMargin);
    }

    private static PlatformAdminAlertCountsResponse ToResponse(this PlatformAdminAlertCountsDto alerts)
    {
        return new PlatformAdminAlertCountsResponse(
            alerts.LowCreditWorkspaces,
            alerts.NoCreditWorkspaces,
            alerts.TrialsExpiringSoon,
            alerts.PaymentIssues,
            alerts.StripeWebhookIssues);
    }

    private static PlatformAdminUsageWindowResponse ToResponse(this PlatformAdminUsageWindowDto usageWindow)
    {
        return new PlatformAdminUsageWindowResponse(
            usageWindow.Key,
            usageWindow.WorkspaceCount,
            usageWindow.EventCount,
            usageWindow.PromptTokens,
            usageWindow.CompletionTokens,
            usageWindow.TotalTokens,
            usageWindow.CreditsCharged,
            usageWindow.EstimatedProviderCost,
            usageWindow.EstimatedRevenueBasis,
            usageWindow.EstimatedGrossMargin);
    }

    private static PlatformAdminWorkspaceSummaryResponse ToResponse(this PlatformAdminWorkspaceSummaryDto workspace)
    {
        return new PlatformAdminWorkspaceSummaryResponse(
            workspace.WorkspaceId,
            workspace.WorkspaceName,
            workspace.WorkspaceSlug,
            workspace.PlanName,
            workspace.SubscriptionStatus,
            workspace.MemberCount,
            workspace.AvailableCredits,
            workspace.ConsumedCredits,
            workspace.TotalTrackedCredits,
            workspace.RemainingRatio,
            workspace.RemainingPercent,
            workspace.TrialEndsAtUtc,
            workspace.LastUsageAtUtc,
            workspace.CreditsCharged,
            workspace.TotalTokens,
            workspace.EstimatedProviderCost,
            workspace.EstimatedRevenueBasis,
            workspace.EstimatedGrossMargin);
    }

    private static PlatformAdminModelUsageResponse ToResponse(this PlatformAdminModelUsageDto modelUsage)
    {
        return new PlatformAdminModelUsageResponse(
            modelUsage.Provider,
            modelUsage.Model,
            modelUsage.WorkspaceCount,
            modelUsage.EventCount,
            modelUsage.CreditsCharged,
            modelUsage.TotalTokens,
            modelUsage.EstimatedProviderCost,
            modelUsage.EstimatedRevenueBasis,
            modelUsage.EstimatedGrossMargin,
            modelUsage.LastUsedAtUtc);
    }

    private static PlatformAdminBillingTransactionResponse ToResponse(this PlatformAdminBillingTransactionDto transaction)
    {
        return new PlatformAdminBillingTransactionResponse(
            transaction.TransactionId,
            transaction.WorkspaceId,
            transaction.WorkspaceName,
            transaction.WorkspaceSlug,
            transaction.Type,
            transaction.Status,
            transaction.PlanName,
            transaction.Credits,
            transaction.Amount,
            transaction.CurrencyCode,
            transaction.OccurredAtUtc);
    }

    private static PlatformAdminSubscriptionActivityResponse ToResponse(this PlatformAdminSubscriptionActivityDto subscription)
    {
        return new PlatformAdminSubscriptionActivityResponse(
            subscription.WorkspaceId,
            subscription.WorkspaceName,
            subscription.WorkspaceSlug,
            subscription.PlanName,
            subscription.Status,
            subscription.UpdatedAtUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.TrialEndsAtUtc);
    }

    private static PlatformAdminPaymentIssueResponse ToResponse(this PlatformAdminPaymentIssueDto paymentIssue)
    {
        return new PlatformAdminPaymentIssueResponse(
            paymentIssue.TransactionId,
            paymentIssue.WorkspaceId,
            paymentIssue.WorkspaceName,
            paymentIssue.WorkspaceSlug,
            paymentIssue.Type,
            paymentIssue.Status,
            paymentIssue.Amount,
            paymentIssue.CurrencyCode,
            paymentIssue.CreatedAtUtc,
            paymentIssue.UpdatedAtUtc);
    }

    private static PlatformAdminStripeWebhookIssueResponse ToResponse(this PlatformAdminStripeWebhookIssueDto webhookIssue)
    {
        return new PlatformAdminStripeWebhookIssueResponse(
            webhookIssue.WebhookEventId,
            webhookIssue.ProviderEventId,
            webhookIssue.EventType,
            webhookIssue.Status,
            webhookIssue.AttemptCount,
            webhookIssue.FirstReceivedAtUtc,
            webhookIssue.LastReceivedAtUtc,
            webhookIssue.UpdatedAtUtc,
            webhookIssue.LastError);
    }
}
