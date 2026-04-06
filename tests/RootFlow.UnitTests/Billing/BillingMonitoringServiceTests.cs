using Microsoft.Extensions.Logging.Abstractions;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing;
using RootFlow.Application.PlatformAdmin;
using RootFlow.Application.PlatformAdmin.Dtos;
using RootFlow.Domain.Billing;
using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Billing;

public sealed class BillingMonitoringServiceTests
{
    [Fact]
    public async Task RunAsync_SendsPlatformAlertsAndWorkspaceLifecycleNotifications_WithDedupe()
    {
        var utcNow = new DateTime(2026, 4, 6, 12, 0, 0, DateTimeKind.Utc);
        var trialWorkspaceId = Guid.NewGuid();
        var lowCreditWorkspaceId = Guid.NewGuid();
        var platformAdminRepository = new FakePlatformAdminRepository(
            workspaceSummaries:
            [
                new PlatformAdminWorkspaceSummaryDto(
                    trialWorkspaceId,
                    "Trial Workspace",
                    "trial-workspace",
                    "Starter",
                    "Trial",
                    1,
                    5_000,
                    0,
                    5_000,
                    1.0m,
                    100m,
                    utcNow.AddDays(2),
                    utcNow.AddHours(-2),
                    0,
                    0,
                    0m,
                    0m,
                    0m),
                new PlatformAdminWorkspaceSummaryDto(
                    lowCreditWorkspaceId,
                    "Low Credit Workspace",
                    "low-credit-workspace",
                    "Pro",
                    "Active",
                    2,
                    75,
                    925,
                    1_000,
                    0.075m,
                    7.5m,
                    null,
                    utcNow.AddMinutes(-20),
                    500,
                    10_000,
                    12.50m,
                    18.75m,
                    6.25m)
            ],
            paymentIssues:
            [
                new PlatformAdminPaymentIssueDto(
                    Guid.NewGuid(),
                    lowCreditWorkspaceId,
                    "Low Credit Workspace",
                    "low-credit-workspace",
                    "SubscriptionCheckout",
                    "Failed",
                    199.90m,
                    "BRL",
                    utcNow.AddHours(-1),
                    utcNow.AddMinutes(-10))
            ]);
        var workspaceBillingRepository = new FakeWorkspaceBillingRepository(
            webhookIssues:
            [
                new WorkspaceBillingWebhookEvent(
                    Guid.NewGuid(),
                    "stripe",
                    "evt_123",
                    "checkout.session.completed",
                    "{\"id\":\"evt_123\"}",
                    "signature",
                    WorkspaceBillingWebhookEventStatus.Failed,
                    2,
                    utcNow.AddHours(-1),
                    utcNow.AddMinutes(-30),
                    utcNow.AddMinutes(-30),
                    lastError: "Stripe snapshot could not be resolved.")
            ]);
        var membershipRepository = new FakeWorkspaceMembershipRepository();
        membershipRepository.AddOwner(trialWorkspaceId, "Trial Owner", "trial-owner@rootflow.test", utcNow.AddDays(-5));
        membershipRepository.AddOwner(lowCreditWorkspaceId, "Low Owner", "low-owner@rootflow.test", utcNow.AddDays(-10));

        var billingOptions = new WorkspaceBillingOptions
        {
            CreditsPerDollar = 100
        };
        var platformAdminOptions = new PlatformAdminOptions
        {
            Emails = ["admin@rootflow.test"],
            LowCreditThresholdRatio = 0.40m,
            TrialExpiringWithinDays = 3,
            DashboardListSize = 5,
            ModelBreakdownSize = 5
        };
        var clock = new FrozenClock(utcNow);
        var dashboardService = new PlatformAdminDashboardService(
            platformAdminRepository,
            workspaceBillingRepository,
            billingOptions,
            platformAdminOptions,
            clock);
        var notifier = new FakeWorkspaceBillingNotifier();
        var service = new BillingMonitoringService(
            dashboardService,
            workspaceBillingRepository,
            membershipRepository,
            notifier,
            platformAdminOptions,
            clock,
            NullLogger<BillingMonitoringService>.Instance);

        var firstRun = await service.RunAsync();
        var secondRun = await service.RunAsync();

        Assert.Equal(1, firstRun.AdminAlertsSent);
        Assert.Equal(2, firstRun.WorkspaceNotificationsSent);
        Assert.Equal(1, firstRun.PaymentIssueCount);
        Assert.Equal(1, firstRun.ReplayableWebhookCount);
        Assert.Equal(0, secondRun.AdminAlertsSent);
        Assert.Equal(0, secondRun.WorkspaceNotificationsSent);
        Assert.Single(notifier.PlatformAlerts);
        Assert.Equal(2, notifier.LifecycleNotifications.Count);
        Assert.Contains(
            notifier.LifecycleNotifications,
            notification => notification.Kind == WorkspaceBillingLifecycleNotificationKind.TrialExpiring);
        Assert.Contains(
            notifier.LifecycleNotifications,
            notification => notification.Kind == WorkspaceBillingLifecycleNotificationKind.CriticalCredits);
    }

    private sealed class FakePlatformAdminRepository : IPlatformAdminRepository
    {
        private readonly IReadOnlyList<PlatformAdminWorkspaceSummaryDto> _workspaceSummaries;
        private readonly IReadOnlyList<PlatformAdminPaymentIssueDto> _paymentIssues;

        public FakePlatformAdminRepository(
            IReadOnlyList<PlatformAdminWorkspaceSummaryDto> workspaceSummaries,
            IReadOnlyList<PlatformAdminPaymentIssueDto> paymentIssues)
        {
            _workspaceSummaries = workspaceSummaries;
            _paymentIssues = paymentIssues;
        }

        public Task<PlatformAdminOverviewDto> GetOverviewAsync(
            decimal creditsPerCurrencyUnit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new PlatformAdminOverviewDto(
                _workspaceSummaries.Count,
                _workspaceSummaries.Count(workspace => string.Equals(workspace.SubscriptionStatus, "Active", StringComparison.OrdinalIgnoreCase)),
                _workspaceSummaries.Count(workspace => string.Equals(workspace.SubscriptionStatus, "Trial", StringComparison.OrdinalIgnoreCase)),
                _workspaceSummaries.Sum(workspace => workspace.MemberCount),
                _workspaceSummaries.Sum(workspace => workspace.AvailableCredits),
                _workspaceSummaries.Sum(workspace => workspace.ConsumedCredits),
                _workspaceSummaries.Sum(workspace => workspace.EstimatedProviderCost),
                _workspaceSummaries.Sum(workspace => workspace.EstimatedRevenueBasis),
                _workspaceSummaries.Sum(workspace => workspace.EstimatedGrossMargin)));
        }

        public Task<IReadOnlyList<PlatformAdminUsageWindowDto>> ListUsageWindowsAsync(
            decimal creditsPerCurrencyUnit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformAdminUsageWindowDto>>([]);
        }

        public Task<IReadOnlyList<PlatformAdminWorkspaceSummaryDto>> ListWorkspaceSummariesAsync(
            decimal creditsPerCurrencyUnit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_workspaceSummaries);
        }

        public Task<IReadOnlyList<PlatformAdminModelUsageDto>> ListModelBreakdownAsync(
            int take,
            decimal creditsPerCurrencyUnit,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformAdminModelUsageDto>>([]);
        }

        public Task<IReadOnlyList<PlatformAdminBillingTransactionDto>> ListRecentCreditPurchasesAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformAdminBillingTransactionDto>>([]);
        }

        public Task<IReadOnlyList<PlatformAdminSubscriptionActivityDto>> ListRecentSubscriptionChangesAsync(
            int take,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformAdminSubscriptionActivityDto>>([]);
        }

        public Task<IReadOnlyList<PlatformAdminPaymentIssueDto>> ListPaymentIssuesAsync(
            int take,
            int pendingThresholdMinutes,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<PlatformAdminPaymentIssueDto>>(_paymentIssues.Take(take).ToArray());
        }
    }

    private sealed class FakeWorkspaceBillingRepository : IWorkspaceBillingRepository
    {
        private readonly IReadOnlyList<WorkspaceBillingWebhookEvent> _webhookIssues;
        private readonly HashSet<string> _notificationDeliveries = new(StringComparer.OrdinalIgnoreCase);

        public FakeWorkspaceBillingRepository(IReadOnlyList<WorkspaceBillingWebhookEvent> webhookIssues)
        {
            _webhookIssues = webhookIssues;
        }

        public Task<IReadOnlyList<WorkspaceBillingWebhookEvent>> ListReplayableBillingWebhookEventsAsync(
            string provider,
            int take,
            DateTime failedBeforeUtc,
            DateTime staleProcessingBeforeUtc,
            CancellationToken cancellationToken = default)
        {
            var results = _webhookIssues
                .Where(issue => string.Equals(issue.Provider, provider, StringComparison.OrdinalIgnoreCase))
                .Take(take)
                .ToArray();
            return Task.FromResult<IReadOnlyList<WorkspaceBillingWebhookEvent>>(results);
        }

        public Task<bool> BillingNotificationDeliveryExistsAsync(
            string notificationKind,
            string dedupeKey,
            string recipientEmail,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_notificationDeliveries.Contains(BuildDeliveryKey(notificationKind, dedupeKey, recipientEmail)));
        }

        public Task RecordBillingNotificationDeliveryAsync(
            WorkspaceBillingNotificationDelivery delivery,
            CancellationToken cancellationToken = default)
        {
            _notificationDeliveries.Add(BuildDeliveryKey(delivery.NotificationKind, delivery.DedupeKey, delivery.RecipientEmail));
            return Task.CompletedTask;
        }

        public Task EnsureProvisionedAsync(
            WorkspaceSubscription subscription,
            WorkspaceCreditBalance balance,
            WorkspaceCreditLedgerEntry? initialGrantEntry,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceSubscription?> GetCurrentSubscriptionAsync(
            Guid workspaceId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceSubscription?> GetLatestSubscriptionAsync(
            Guid workspaceId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceSubscription?> GetSubscriptionByProviderSubscriptionIdAsync(
            string provider,
            string providerSubscriptionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceSubscription?> GetLatestSubscriptionByProviderCustomerIdAsync(
            string provider,
            string providerCustomerId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<int> UpdateSubscriptionAsync(
            WorkspaceSubscription subscription,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceCreditBalance?> GetCreditBalanceAsync(
            Guid workspaceId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceCreditBalance> AppendLedgerEntryAsync(
            WorkspaceCreditLedgerEntry entry,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> LedgerReferenceExistsAsync(
            string referenceType,
            string referenceId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<long> EnsureCreditGrantTargetAsync(
            Guid workspaceId,
            WorkspaceCreditLedgerType type,
            long targetAmount,
            string description,
            DateTime createdAtUtc,
            string referenceType,
            string referenceId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddUsageEventAsync(
            WorkspaceUsageEvent usageEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceCreditBalance> RecordUsageAsync(
            WorkspaceUsageEvent usageEvent,
            WorkspaceCreditLedgerEntry debitEntry,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkspaceCreditLedgerEntry>> ListLedgerEntriesAsync(
            Guid workspaceId,
            int take = 100,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<IReadOnlyList<WorkspaceUsageEvent>> ListUsageEventsAsync(
            Guid workspaceId,
            int take = 100,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task AddBillingTransactionAsync(
            WorkspaceBillingTransaction transaction,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceBillingTransaction?> GetBillingTransactionByCheckoutSessionIdAsync(
            string provider,
            string externalCheckoutSessionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceBillingTransaction?> GetBillingTransactionByInvoiceIdAsync(
            string provider,
            string externalInvoiceId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceBillingTransaction?> GetLatestBillingTransactionBySubscriptionIdAsync(
            string provider,
            string externalSubscriptionId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceBillingTransaction?> GetLatestPendingBillingTransactionByCustomerIdAsync(
            string provider,
            string externalCustomerId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task UpdateBillingTransactionAsync(
            WorkspaceBillingTransaction transaction,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceBillingWebhookEvent> UpsertBillingWebhookEventAsync(
            WorkspaceBillingWebhookEvent webhookEvent,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<WorkspaceBillingWebhookEvent?> GetBillingWebhookEventByProviderEventIdAsync(
            string provider,
            string providerEventId,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task<bool> TryStartBillingWebhookEventProcessingAsync(
            Guid webhookEventId,
            DateTime startedAtUtc,
            DateTime? staleProcessingBeforeUtc = null,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkBillingWebhookEventProcessedAsync(
            Guid webhookEventId,
            DateTime processedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        public Task MarkBillingWebhookEventFailedAsync(
            Guid webhookEventId,
            string error,
            DateTime failedAtUtc,
            CancellationToken cancellationToken = default) => throw new NotSupportedException();

        private static string BuildDeliveryKey(string notificationKind, string dedupeKey, string recipientEmail)
        {
            return $"{notificationKind.Trim()}::{dedupeKey.Trim()}::{recipientEmail.Trim().ToUpperInvariant()}";
        }
    }

    private sealed class FakeWorkspaceMembershipRepository : IWorkspaceMembershipRepository
    {
        private readonly Dictionary<Guid, List<WorkspaceMemberRecord>> _members = [];

        public void AddOwner(Guid workspaceId, string fullName, string email, DateTime createdAtUtc)
        {
            if (!_members.TryGetValue(workspaceId, out var members))
            {
                members = [];
                _members[workspaceId] = members;
            }

            members.Add(new WorkspaceMemberRecord(Guid.NewGuid(), fullName, email, WorkspaceRole.Owner, createdAtUtc, true));
        }

        public Task<WorkspaceMembership?> GetAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task AddAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task UpdateAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default)
        {
            throw new NotSupportedException();
        }

        public Task<IReadOnlyList<WorkspaceMemberRecord>> ListByWorkspaceAsync(
            Guid workspaceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkspaceMemberRecord>>(
                _members.TryGetValue(workspaceId, out var members)
                    ? members.ToArray()
                    : []);
        }
    }

    private sealed class FakeWorkspaceBillingNotifier : IWorkspaceBillingNotifier
    {
        public List<WorkspacePaymentConfirmationNotification> PaymentConfirmations { get; } = [];

        public List<WorkspaceBillingLifecycleNotification> LifecycleNotifications { get; } = [];

        public List<PlatformBillingAlertNotification> PlatformAlerts { get; } = [];

        public Task SendPaymentConfirmedAsync(
            WorkspacePaymentConfirmationNotification notification,
            CancellationToken cancellationToken = default)
        {
            PaymentConfirmations.Add(notification);
            return Task.CompletedTask;
        }

        public Task SendLifecycleNotificationAsync(
            WorkspaceBillingLifecycleNotification notification,
            CancellationToken cancellationToken = default)
        {
            LifecycleNotifications.Add(notification);
            return Task.CompletedTask;
        }

        public Task SendPlatformAlertAsync(
            PlatformBillingAlertNotification notification,
            CancellationToken cancellationToken = default)
        {
            PlatformAlerts.Add(notification);
            return Task.CompletedTask;
        }
    }

    private sealed class FrozenClock : IClock
    {
        private readonly DateTime _utcNow;

        public FrozenClock(DateTime utcNow)
        {
            _utcNow = utcNow;
        }

        public DateTime UtcNow => _utcNow;
    }
}
