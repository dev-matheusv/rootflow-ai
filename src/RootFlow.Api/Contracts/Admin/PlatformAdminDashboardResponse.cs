namespace RootFlow.Api.Contracts.Admin;

public sealed record PlatformAdminDashboardResponse(
    PlatformAdminOverviewResponse Overview,
    PlatformAdminAlertCountsResponse Alerts,
    PlatformAdminBillingOpsReadinessResponse BillingOpsReadiness,
    IReadOnlyList<PlatformAdminUsageWindowResponse> UsageWindows,
    IReadOnlyList<PlatformAdminWorkspaceSummaryResponse> LowCreditWorkspaces,
    IReadOnlyList<PlatformAdminWorkspaceSummaryResponse> NoCreditWorkspaces,
    IReadOnlyList<PlatformAdminWorkspaceSummaryResponse> TrialsExpiringSoon,
    IReadOnlyList<PlatformAdminPaymentIssueResponse> PaymentIssues,
    IReadOnlyList<PlatformAdminStripeWebhookIssueResponse> StripeWebhookIssues,
    IReadOnlyList<PlatformAdminBillingTransactionResponse> RecentCreditPurchases,
    IReadOnlyList<PlatformAdminSubscriptionActivityResponse> RecentSubscriptionChanges,
    IReadOnlyList<PlatformAdminWorkspaceSummaryResponse> TopCreditConsumers,
    IReadOnlyList<PlatformAdminWorkspaceSummaryResponse> TopProviderCostWorkspaces,
    IReadOnlyList<PlatformAdminWorkspaceSummaryResponse> TopRevenueBasisWorkspaces,
    IReadOnlyList<PlatformAdminModelUsageResponse> ModelBreakdown);

public sealed record PlatformAdminOverviewResponse(
    int TotalWorkspaces,
    int TotalActiveSubscriptions,
    int TotalTrials,
    int TotalUsers,
    long TotalAvailableCredits,
    long TotalConsumedCredits,
    decimal EstimatedProviderCost,
    decimal EstimatedRevenueBasis,
    decimal EstimatedGrossMargin);

public sealed record PlatformAdminAlertCountsResponse(
    int LowCreditWorkspaces,
    int NoCreditWorkspaces,
    int TrialsExpiringSoon,
    int PaymentIssues,
    int StripeWebhookIssues);

public sealed record PlatformAdminBillingOpsReadinessResponse(
    bool IsReady,
    int AdminAlertRecipientCount,
    bool AdminAlertRecipientsConfigured,
    bool OutboundEmailConfigured,
    bool BackgroundMonitoringEnabled);

public sealed record PlatformAdminUsageWindowResponse(
    string Key,
    int WorkspaceCount,
    int EventCount,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    long CreditsCharged,
    decimal EstimatedProviderCost,
    decimal EstimatedRevenueBasis,
    decimal EstimatedGrossMargin);

public sealed record PlatformAdminWorkspaceSummaryResponse(
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string? PlanName,
    string SubscriptionStatus,
    int MemberCount,
    long AvailableCredits,
    long ConsumedCredits,
    long TotalTrackedCredits,
    decimal RemainingRatio,
    decimal RemainingPercent,
    DateTime? TrialEndsAtUtc,
    DateTime? LastUsageAtUtc,
    long CreditsCharged,
    long TotalTokens,
    decimal EstimatedProviderCost,
    decimal EstimatedRevenueBasis,
    decimal EstimatedGrossMargin);

public sealed record PlatformAdminModelUsageResponse(
    string Provider,
    string Model,
    int WorkspaceCount,
    int EventCount,
    long CreditsCharged,
    long TotalTokens,
    decimal EstimatedProviderCost,
    decimal EstimatedRevenueBasis,
    decimal EstimatedGrossMargin,
    DateTime? LastUsedAtUtc);

public sealed record PlatformAdminBillingTransactionResponse(
    Guid TransactionId,
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string Type,
    string Status,
    string? PlanName,
    long? Credits,
    decimal Amount,
    string CurrencyCode,
    DateTime OccurredAtUtc);

public sealed record PlatformAdminSubscriptionActivityResponse(
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string? PlanName,
    string Status,
    DateTime UpdatedAtUtc,
    DateTime CurrentPeriodEndUtc,
    DateTime? TrialEndsAtUtc);

public sealed record PlatformAdminPaymentIssueResponse(
    Guid TransactionId,
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string Type,
    string Status,
    decimal Amount,
    string CurrencyCode,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record PlatformAdminStripeWebhookIssueResponse(
    Guid WebhookEventId,
    string ProviderEventId,
    string EventType,
    string Status,
    int AttemptCount,
    DateTime FirstReceivedAtUtc,
    DateTime LastReceivedAtUtc,
    DateTime UpdatedAtUtc,
    string? LastError);
