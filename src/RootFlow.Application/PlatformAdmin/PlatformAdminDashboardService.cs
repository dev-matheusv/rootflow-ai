using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing;
using RootFlow.Application.PlatformAdmin.Dtos;
using RootFlow.Application.PlatformAdmin.Queries;
using RootFlow.Domain.Billing;

namespace RootFlow.Application.PlatformAdmin;

public sealed class PlatformAdminDashboardService
{
    private readonly IPlatformAdminRepository _platformAdminRepository;
    private readonly IWorkspaceBillingRepository _workspaceBillingRepository;
    private readonly WorkspaceBillingOptions _billingOptions;
    private readonly PlatformAdminOptions _platformAdminOptions;
    private readonly IClock _clock;

    public PlatformAdminDashboardService(
        IPlatformAdminRepository platformAdminRepository,
        IWorkspaceBillingRepository workspaceBillingRepository,
        WorkspaceBillingOptions billingOptions,
        PlatformAdminOptions platformAdminOptions,
        IClock clock)
    {
        _platformAdminRepository = platformAdminRepository;
        _workspaceBillingRepository = workspaceBillingRepository;
        _billingOptions = billingOptions;
        _platformAdminOptions = platformAdminOptions;
        _clock = clock;
    }

    public async Task<PlatformAdminDashboardDto> GetDashboardAsync(
        GetPlatformAdminDashboardQuery query,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(query);

        var creditsPerCurrencyUnit = ResolveCreditsPerCurrencyUnit();
        var listSize = Math.Max(1, _platformAdminOptions.DashboardListSize);
        var modelBreakdownSize = Math.Max(1, _platformAdminOptions.ModelBreakdownSize);

        var overview = await _platformAdminRepository.GetOverviewAsync(creditsPerCurrencyUnit, cancellationToken);
        var usageWindows = await _platformAdminRepository.ListUsageWindowsAsync(creditsPerCurrencyUnit, cancellationToken);
        var workspaceSummaries = await _platformAdminRepository.ListWorkspaceSummariesAsync(creditsPerCurrencyUnit, cancellationToken);
        var modelBreakdown = await _platformAdminRepository.ListModelBreakdownAsync(modelBreakdownSize, creditsPerCurrencyUnit, cancellationToken);
        var recentCreditPurchases = await _platformAdminRepository.ListRecentCreditPurchasesAsync(listSize, cancellationToken);
        var recentSubscriptionChanges = await _platformAdminRepository.ListRecentSubscriptionChangesAsync(listSize, cancellationToken);
        var paymentIssues = await _platformAdminRepository.ListPaymentIssuesAsync(
            listSize,
            Math.Max(1, _platformAdminOptions.PendingPaymentAnomalyMinutes),
            cancellationToken);

        var lowCreditThresholdRatio = ClampRatio(_platformAdminOptions.LowCreditThresholdRatio);
        var utcNow = _clock.UtcNow;
        var trialsExpiringThresholdUtc = utcNow.AddDays(Math.Max(1, _platformAdminOptions.TrialExpiringWithinDays));
        var stripeWebhookIssues = await _workspaceBillingRepository.ListReplayableBillingWebhookEventsAsync(
            "stripe",
            listSize,
            utcNow,
            utcNow,
            cancellationToken);

        var lowCreditWorkspaces = workspaceSummaries
            .Where(workspace => workspace.AvailableCredits > 0 && workspace.TotalTrackedCredits > 0 && workspace.RemainingRatio <= lowCreditThresholdRatio)
            .OrderBy(workspace => workspace.RemainingRatio)
            .ThenBy(workspace => workspace.AvailableCredits)
            .Take(listSize)
            .ToArray();

        var noCreditWorkspaces = workspaceSummaries
            .Where(workspace => workspace.AvailableCredits <= 0)
            .OrderByDescending(workspace => workspace.LastUsageAtUtc)
            .ThenBy(workspace => workspace.WorkspaceName, StringComparer.OrdinalIgnoreCase)
            .Take(listSize)
            .ToArray();

        var trialsExpiringSoon = workspaceSummaries
            .Where(workspace =>
                string.Equals(workspace.SubscriptionStatus, "Trial", StringComparison.OrdinalIgnoreCase) &&
                workspace.TrialEndsAtUtc.HasValue &&
                workspace.TrialEndsAtUtc.Value > utcNow &&
                workspace.TrialEndsAtUtc.Value <= trialsExpiringThresholdUtc)
            .OrderBy(workspace => workspace.TrialEndsAtUtc)
            .Take(listSize)
            .ToArray();

        var topCreditConsumers = workspaceSummaries
            .Where(workspace => workspace.CreditsCharged > 0)
            .OrderByDescending(workspace => workspace.CreditsCharged)
            .ThenByDescending(workspace => workspace.EstimatedProviderCost)
            .Take(listSize)
            .ToArray();

        var topProviderCostWorkspaces = workspaceSummaries
            .Where(workspace => workspace.EstimatedProviderCost > 0m)
            .OrderByDescending(workspace => workspace.EstimatedProviderCost)
            .ThenByDescending(workspace => workspace.CreditsCharged)
            .Take(listSize)
            .ToArray();

        var topRevenueBasisWorkspaces = workspaceSummaries
            .Where(workspace => workspace.EstimatedRevenueBasis > 0m)
            .OrderByDescending(workspace => workspace.EstimatedRevenueBasis)
            .ThenByDescending(workspace => workspace.EstimatedProviderCost)
            .Take(listSize)
            .ToArray();

        return new PlatformAdminDashboardDto(
            overview,
            new PlatformAdminAlertCountsDto(
                lowCreditWorkspaces.Length,
                noCreditWorkspaces.Length,
                trialsExpiringSoon.Length,
                paymentIssues.Count,
                stripeWebhookIssues.Count),
            usageWindows,
            lowCreditWorkspaces,
            noCreditWorkspaces,
            trialsExpiringSoon,
            paymentIssues,
            stripeWebhookIssues.Select(MapStripeWebhookIssue).ToArray(),
            recentCreditPurchases,
            recentSubscriptionChanges,
            topCreditConsumers,
            topProviderCostWorkspaces,
            topRevenueBasisWorkspaces,
            modelBreakdown);
    }

    private static PlatformAdminStripeWebhookIssueDto MapStripeWebhookIssue(WorkspaceBillingWebhookEvent webhookIssue)
    {
        return new PlatformAdminStripeWebhookIssueDto(
            webhookIssue.Id,
            webhookIssue.ProviderEventId,
            webhookIssue.EventType,
            webhookIssue.Status.ToString(),
            webhookIssue.AttemptCount,
            webhookIssue.FirstReceivedAtUtc,
            webhookIssue.LastReceivedAtUtc,
            webhookIssue.UpdatedAtUtc,
            webhookIssue.LastError);
    }

    private decimal ResolveCreditsPerCurrencyUnit()
    {
        return _billingOptions.CreditsPerDollar <= 0
            ? 100m
            : _billingOptions.CreditsPerDollar;
    }

    private static decimal ClampRatio(decimal ratio)
    {
        if (ratio < 0m)
        {
            return 0m;
        }

        if (ratio > 1m)
        {
            return 1m;
        }

        return ratio;
    }
}
