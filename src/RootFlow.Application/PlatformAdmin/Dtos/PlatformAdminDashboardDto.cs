namespace RootFlow.Application.PlatformAdmin.Dtos;

public sealed record PlatformAdminDashboardDto(
    PlatformAdminOverviewDto Overview,
    PlatformAdminAlertCountsDto Alerts,
    IReadOnlyList<PlatformAdminUsageWindowDto> UsageWindows,
    IReadOnlyList<PlatformAdminWorkspaceSummaryDto> LowCreditWorkspaces,
    IReadOnlyList<PlatformAdminWorkspaceSummaryDto> NoCreditWorkspaces,
    IReadOnlyList<PlatformAdminWorkspaceSummaryDto> TrialsExpiringSoon,
    IReadOnlyList<PlatformAdminPaymentIssueDto> PaymentIssues,
    IReadOnlyList<PlatformAdminStripeWebhookIssueDto> StripeWebhookIssues,
    IReadOnlyList<PlatformAdminBillingTransactionDto> RecentCreditPurchases,
    IReadOnlyList<PlatformAdminSubscriptionActivityDto> RecentSubscriptionChanges,
    IReadOnlyList<PlatformAdminWorkspaceSummaryDto> TopCreditConsumers,
    IReadOnlyList<PlatformAdminWorkspaceSummaryDto> TopProviderCostWorkspaces,
    IReadOnlyList<PlatformAdminWorkspaceSummaryDto> TopRevenueBasisWorkspaces,
    IReadOnlyList<PlatformAdminModelUsageDto> ModelBreakdown);

public sealed record PlatformAdminOverviewDto(
    int TotalWorkspaces,
    int TotalActiveSubscriptions,
    int TotalTrials,
    int TotalUsers,
    long TotalAvailableCredits,
    long TotalConsumedCredits,
    decimal EstimatedProviderCost,
    decimal EstimatedRevenueBasis,
    decimal EstimatedGrossMargin);

public sealed record PlatformAdminAlertCountsDto(
    int LowCreditWorkspaces,
    int NoCreditWorkspaces,
    int TrialsExpiringSoon,
    int PaymentIssues,
    int StripeWebhookIssues);

public sealed record PlatformAdminUsageWindowDto(
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

public sealed record PlatformAdminWorkspaceSummaryDto(
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

public sealed record PlatformAdminModelUsageDto(
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

public sealed record PlatformAdminBillingTransactionDto(
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

public sealed record PlatformAdminSubscriptionActivityDto(
    Guid WorkspaceId,
    string WorkspaceName,
    string WorkspaceSlug,
    string? PlanName,
    string Status,
    DateTime UpdatedAtUtc,
    DateTime CurrentPeriodEndUtc,
    DateTime? TrialEndsAtUtc);

public sealed record PlatformAdminPaymentIssueDto(
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

public sealed record PlatformAdminStripeWebhookIssueDto(
    Guid WebhookEventId,
    string ProviderEventId,
    string EventType,
    string Status,
    int AttemptCount,
    DateTime FirstReceivedAtUtc,
    DateTime LastReceivedAtUtc,
    DateTime UpdatedAtUtc,
    string? LastError);
