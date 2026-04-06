using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.PlatformAdmin;
using RootFlow.Application.PlatformAdmin.Dtos;
using RootFlow.Application.PlatformAdmin.Queries;
using RootFlow.Domain.Billing;
using RootFlow.Domain.Workspaces;
using Microsoft.Extensions.Logging;

namespace RootFlow.Application.Billing;

public sealed class BillingMonitoringService
{
    private const string StripeProvider = "stripe";
    private static readonly TimeSpan WebhookAlertDelay = TimeSpan.FromMinutes(3);
    private readonly PlatformAdminDashboardService _platformAdminDashboardService;
    private readonly IWorkspaceBillingRepository _workspaceBillingRepository;
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;
    private readonly IWorkspaceBillingNotifier _workspaceBillingNotifier;
    private readonly PlatformAdminOptions _platformAdminOptions;
    private readonly IClock _clock;
    private readonly ILogger<BillingMonitoringService> _logger;

    public BillingMonitoringService(
        PlatformAdminDashboardService platformAdminDashboardService,
        IWorkspaceBillingRepository workspaceBillingRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository,
        IWorkspaceBillingNotifier workspaceBillingNotifier,
        PlatformAdminOptions platformAdminOptions,
        IClock clock,
        ILogger<BillingMonitoringService> logger)
    {
        _platformAdminDashboardService = platformAdminDashboardService;
        _workspaceBillingRepository = workspaceBillingRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
        _workspaceBillingNotifier = workspaceBillingNotifier;
        _platformAdminOptions = platformAdminOptions;
        _clock = clock;
        _logger = logger;
    }

    public async Task<BillingMonitoringRunResult> RunAsync(
        CancellationToken cancellationToken = default)
    {
        var utcNow = _clock.UtcNow;
        var dashboard = await _platformAdminDashboardService.GetDashboardAsync(
            new GetPlatformAdminDashboardQuery(),
            cancellationToken);
        var webhookIssues = await _workspaceBillingRepository.ListReplayableBillingWebhookEventsAsync(
            StripeProvider,
            take: 20,
            failedBeforeUtc: utcNow,
            staleProcessingBeforeUtc: utcNow,
            cancellationToken);
        var actionableWebhookIssues = ResolveActionableWebhookIssues(webhookIssues, utcNow);

        var adminAlertsSent = await SendPlatformAlertsAsync(
            dashboard.PaymentIssues,
            actionableWebhookIssues,
            utcNow,
            cancellationToken);
        var lifecycleNotificationsSent = await SendWorkspaceLifecycleNotificationsAsync(
            dashboard,
            utcNow,
            cancellationToken);

        return new BillingMonitoringRunResult(
            adminAlertsSent,
            lifecycleNotificationsSent,
            dashboard.PaymentIssues.Count,
            actionableWebhookIssues.Count);
    }

    private async Task<int> SendPlatformAlertsAsync(
        IReadOnlyList<PlatformAdminPaymentIssueDto> paymentIssues,
        IReadOnlyList<WorkspaceBillingWebhookEvent> webhookIssues,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (paymentIssues.Count == 0 && webhookIssues.Count == 0)
        {
            return 0;
        }

        var recipients = _platformAdminOptions.Emails
            .Select(email => email?.Trim())
            .Where(email => !string.IsNullOrWhiteSpace(email))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (recipients.Length == 0)
        {
            _logger.LogWarning(
                "Platform billing alerts were skipped because ROOTFLOW_PLATFORM_ADMIN_EMAILS is empty.");
            return 0;
        }

        var bucketStartUtc = GetAdminAlertBucketStartUtc(utcNow);
        var dedupeKey = $"platform:{bucketStartUtc:yyyyMMddHH}";
        var detailLines = BuildPlatformAlertDetailLines(paymentIssues, webhookIssues);
        var sentCount = 0;

        foreach (var recipient in recipients)
        {
            if (await _workspaceBillingRepository.BillingNotificationDeliveryExistsAsync(
                    "platform_billing_alert",
                    dedupeKey,
                    recipient!,
                    cancellationToken))
            {
                continue;
            }

            await _workspaceBillingNotifier.SendPlatformAlertAsync(
                new PlatformBillingAlertNotification(
                    recipient!,
                    null,
                    paymentIssues.Count,
                    webhookIssues.Count,
                    detailLines),
                cancellationToken);

            await _workspaceBillingRepository.RecordBillingNotificationDeliveryAsync(
                new WorkspaceBillingNotificationDelivery(
                    Guid.NewGuid(),
                    workspaceId: null,
                    notificationKind: "platform_billing_alert",
                    dedupeKey,
                    recipient!,
                    utcNow),
                cancellationToken);

            sentCount += 1;
        }

        return sentCount;
    }

    private async Task<int> SendWorkspaceLifecycleNotificationsAsync(
        PlatformAdminDashboardDto dashboard,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var sentCount = 0;

        foreach (var workspace in dashboard.TrialsExpiringSoon)
        {
            var recipient = await ResolveWorkspaceRecipientAsync(workspace.WorkspaceId, cancellationToken);
            if (recipient is null)
            {
                continue;
            }

            var dedupeKey = $"trial:{workspace.WorkspaceId}:{utcNow:yyyyMMdd}";
            if (await _workspaceBillingRepository.BillingNotificationDeliveryExistsAsync(
                    "workspace_trial_expiring",
                    dedupeKey,
                    recipient.Email,
                    cancellationToken))
            {
                continue;
            }

            var remainingDays = workspace.TrialEndsAtUtc.HasValue
                ? Math.Max(0, (int)Math.Ceiling((workspace.TrialEndsAtUtc.Value - utcNow).TotalDays))
                : (int?)null;

            await _workspaceBillingNotifier.SendLifecycleNotificationAsync(
                new WorkspaceBillingLifecycleNotification(
                    recipient.Email,
                    recipient.FullName,
                    workspace.WorkspaceName,
                    WorkspaceBillingLifecycleNotificationKind.TrialExpiring,
                    workspace.PlanName,
                    workspace.AvailableCredits,
                    workspace.RemainingPercent > 0 ? (int?)workspace.RemainingPercent : null,
                    workspace.TrialEndsAtUtc,
                    remainingDays),
                cancellationToken);

            await _workspaceBillingRepository.RecordBillingNotificationDeliveryAsync(
                new WorkspaceBillingNotificationDelivery(
                    Guid.NewGuid(),
                    workspace.WorkspaceId,
                    "workspace_trial_expiring",
                    dedupeKey,
                    recipient.Email,
                    utcNow),
                cancellationToken);

            sentCount += 1;
        }

        foreach (var workspace in ResolveLowCreditLifecycleTargets(dashboard))
        {
            var recipient = await ResolveWorkspaceRecipientAsync(workspace.WorkspaceId, cancellationToken);
            if (recipient is null)
            {
                continue;
            }

            var dedupeKey = $"credits:{workspace.WorkspaceId}:{workspace.Kind}:{utcNow:yyyyMMdd}";
            if (await _workspaceBillingRepository.BillingNotificationDeliveryExistsAsync(
                    "workspace_credit_state",
                    dedupeKey,
                    recipient.Email,
                    cancellationToken))
            {
                continue;
            }

            await _workspaceBillingNotifier.SendLifecycleNotificationAsync(
                new WorkspaceBillingLifecycleNotification(
                    recipient.Email,
                    recipient.FullName,
                    workspace.WorkspaceName,
                    workspace.Kind,
                    workspace.PlanName,
                    workspace.AvailableCredits,
                    workspace.RemainingPercent,
                    workspace.TrialEndsAtUtc,
                    null),
                cancellationToken);

            await _workspaceBillingRepository.RecordBillingNotificationDeliveryAsync(
                new WorkspaceBillingNotificationDelivery(
                    Guid.NewGuid(),
                    workspace.WorkspaceId,
                    "workspace_credit_state",
                    dedupeKey,
                    recipient.Email,
                    utcNow),
                cancellationToken);

            sentCount += 1;
        }

        return sentCount;
    }

    private async Task<WorkspaceMemberRecord?> ResolveWorkspaceRecipientAsync(
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var members = await _workspaceMembershipRepository.ListByWorkspaceAsync(workspaceId, cancellationToken);
        return members
            .Where(member => member.IsActive)
            .OrderBy(member => member.Role switch
            {
                WorkspaceRole.Owner => 0,
                WorkspaceRole.Admin => 1,
                _ => 2
            })
            .ThenBy(member => member.CreatedAtUtc)
            .FirstOrDefault();
    }

    private static IReadOnlyList<string> BuildPlatformAlertDetailLines(
        IReadOnlyList<PlatformAdminPaymentIssueDto> paymentIssues,
        IReadOnlyList<WorkspaceBillingWebhookEvent> webhookIssues)
    {
        var detailLines = new List<string>();

        detailLines.Add($"Problemas de pagamento observados: {paymentIssues.Count}");
        detailLines.Add($"Webhooks Stripe pendentes/falhos para replay: {webhookIssues.Count}");

        foreach (var issue in paymentIssues.Take(3))
        {
            detailLines.Add(
                $"Pagamento: {issue.WorkspaceName} (@{issue.WorkspaceSlug}) - {issue.Type}/{issue.Status} - {issue.Amount:N2} {issue.CurrencyCode}");
        }

        foreach (var webhookIssue in webhookIssues.Take(3))
        {
            detailLines.Add(
                $"Webhook: {webhookIssue.EventType} - {webhookIssue.Status} - tentativas {webhookIssue.AttemptCount}");
        }

        return detailLines;
    }

    private static IReadOnlyList<WorkspaceBillingWebhookEvent> ResolveActionableWebhookIssues(
        IReadOnlyList<WorkspaceBillingWebhookEvent> webhookIssues,
        DateTime utcNow)
    {
        return webhookIssues
            .Where(issue => issue.Status switch
            {
                WorkspaceBillingWebhookEventStatus.Failed => true,
                WorkspaceBillingWebhookEventStatus.Pending => issue.LastReceivedAtUtc <= utcNow - WebhookAlertDelay,
                WorkspaceBillingWebhookEventStatus.Processing => issue.ProcessingStartedAtUtc <= utcNow - WebhookAlertDelay,
                _ => false
            })
            .ToArray();
    }

    private static IReadOnlyList<WorkspaceLifecycleTarget> ResolveLowCreditLifecycleTargets(
        PlatformAdminDashboardDto dashboard)
    {
        var targets = new Dictionary<Guid, WorkspaceLifecycleTarget>();

        foreach (var workspace in dashboard.NoCreditWorkspaces)
        {
            targets[workspace.WorkspaceId] = new WorkspaceLifecycleTarget(
                workspace.WorkspaceId,
                workspace.WorkspaceName,
                workspace.PlanName,
                WorkspaceBillingLifecycleNotificationKind.NoCredits,
                workspace.AvailableCredits,
                (int?)workspace.RemainingPercent,
                workspace.TrialEndsAtUtc);
        }

        foreach (var workspace in dashboard.LowCreditWorkspaces)
        {
            if (targets.ContainsKey(workspace.WorkspaceId))
            {
                continue;
            }

            var kind = workspace.RemainingRatio <= 0.15m
                ? WorkspaceBillingLifecycleNotificationKind.CriticalCredits
                : WorkspaceBillingLifecycleNotificationKind.LowCredits;

            targets[workspace.WorkspaceId] = new WorkspaceLifecycleTarget(
                workspace.WorkspaceId,
                workspace.WorkspaceName,
                workspace.PlanName,
                kind,
                workspace.AvailableCredits,
                (int?)workspace.RemainingPercent,
                workspace.TrialEndsAtUtc);
        }

        return targets.Values.ToArray();
    }

    private static DateTime GetAdminAlertBucketStartUtc(DateTime utcNow)
    {
        var bucketHour = (utcNow.Hour / 6) * 6;
        return new DateTime(utcNow.Year, utcNow.Month, utcNow.Day, bucketHour, 0, 0, DateTimeKind.Utc);
    }

    private sealed record WorkspaceLifecycleTarget(
        Guid WorkspaceId,
        string WorkspaceName,
        string? PlanName,
        WorkspaceBillingLifecycleNotificationKind Kind,
        long AvailableCredits,
        int? RemainingPercent,
        DateTime? TrialEndsAtUtc);
}

public sealed record BillingMonitoringRunResult(
    int AdminAlertsSent,
    int WorkspaceNotificationsSent,
    int PaymentIssueCount,
    int ReplayableWebhookCount);
