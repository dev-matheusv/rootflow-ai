using Microsoft.Extensions.Logging.Abstractions;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing;
using RootFlow.Application.Billing.Commands;
using RootFlow.Application.Billing.Queries;
using RootFlow.Domain.Billing;
using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Billing;

public sealed class WorkspaceBillingServiceTests
{
    [Fact]
    public async Task GetCreditSummaryAsync_ProvisionsTrialSubscriptionAndTrialCredits()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = new BillingPlan(
            Guid.NewGuid(),
            "starter",
            "Starter",
            49m,
            "USD",
            10_000,
            3,
            FixedClock.UtcNow);

        var service = CreateService(
            workspaceId,
            starterPlan,
            new StubUsagePricingCalculator(estimatedCost: 0.15m, creditsCharged: 15),
            out var workspaceBillingRepository);

        var summary = await service.GetCreditSummaryAsync(new GetWorkspaceCreditSummaryQuery(workspaceId));

        Assert.NotNull(summary.BillingPlan);
        Assert.Equal("starter", summary.BillingPlan!.Code);
        Assert.NotNull(summary.Subscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Trial, summary.Subscription!.Status);
        Assert.Equal(FixedClock.UtcNow.AddDays(7), summary.Subscription.TrialEndsAtUtc);
        Assert.Equal(5_000, summary.Balance.AvailableCredits);
        Assert.Equal(0, summary.Balance.ConsumedCredits);
        Assert.Single(workspaceBillingRepository.LedgerEntries);
        Assert.Equal(WorkspaceCreditLedgerType.SubscriptionGrant, workspaceBillingRepository.LedgerEntries[0].Type);
    }

    [Fact]
    public async Task RegisterUsageAsync_RecordsUsageAndDebitsWorkspaceCredits()
    {
        var workspaceId = Guid.NewGuid();
        var conversationId = Guid.NewGuid();
        var starterPlan = new BillingPlan(
            Guid.NewGuid(),
            "starter",
            "Starter",
            49m,
            "USD",
            10_000,
            3,
            FixedClock.UtcNow);

        var service = CreateService(
            workspaceId,
            starterPlan,
            new StubUsagePricingCalculator(estimatedCost: 0.32m, creditsCharged: 32),
            out var workspaceBillingRepository);

        await service.GetCreditSummaryAsync(new GetWorkspaceCreditSummaryQuery(workspaceId));

        var usageEvent = await service.RegisterUsageAsync(
            new RegisterWorkspaceUsageCommand(
                workspaceId,
                Guid.NewGuid(),
                conversationId,
                "openai",
                "gpt-4.1-mini",
                400,
                200));

        var updatedBalance = await workspaceBillingRepository.GetCreditBalanceAsync(workspaceId);

        Assert.Equal(0.32m, usageEvent.EstimatedCost);
        Assert.Equal(32, usageEvent.CreditsCharged);
        Assert.NotNull(updatedBalance);
        Assert.Equal(4_968, updatedBalance!.AvailableCredits);
        Assert.Equal(32, updatedBalance.ConsumedCredits);
        Assert.Single(workspaceBillingRepository.UsageEvents);
        Assert.Equal(conversationId, workspaceBillingRepository.UsageEvents[0].ConversationId);
        Assert.Equal(2, workspaceBillingRepository.LedgerEntries.Count);
        Assert.Equal(WorkspaceCreditLedgerType.UsageDebit, workspaceBillingRepository.LedgerEntries[^1].Type);
    }

    [Fact]
    public async Task ConsumeCreditsAsync_Throws_WhenWorkspaceDoesNotHaveEnoughCredits()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = new BillingPlan(
            Guid.NewGuid(),
            "starter",
            "Starter",
            49m,
            "USD",
            10,
            3,
            FixedClock.UtcNow);

        var service = CreateService(
            workspaceId,
            starterPlan,
            new StubUsagePricingCalculator(estimatedCost: 0.01m, creditsCharged: 1),
            out _,
            trialIncludedCredits: 10);

        await service.GetCreditSummaryAsync(new GetWorkspaceCreditSummaryQuery(workspaceId));

        var exception = await Assert.ThrowsAsync<InsufficientWorkspaceCreditsException>(() =>
            service.ConsumeCreditsAsync(
                new ConsumeWorkspaceCreditsCommand(
                    workspaceId,
                    25,
                    WorkspaceCreditLedgerType.ManualAdjustment,
                    "Manual debit")));

        Assert.Contains("enough credits", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task EnsureAssistantUsageAllowedAsync_Throws_WhenTrialHasExpired()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = new BillingPlan(
            Guid.NewGuid(),
            "starter",
            "Starter",
            49m,
            "USD",
            10_000,
            3,
            FixedClock.UtcNow);
        var sharedRepository = new InMemoryWorkspaceBillingRepository();

        var provisioningService = CreateService(
            workspaceId,
            starterPlan,
            new StubUsagePricingCalculator(estimatedCost: 0.01m, creditsCharged: 1),
            out _,
            existingRepository: sharedRepository);

        await provisioningService.GetCreditSummaryAsync(new GetWorkspaceCreditSummaryQuery(workspaceId));

        var expiredTrialService = CreateService(
            workspaceId,
            starterPlan,
            new StubUsagePricingCalculator(estimatedCost: 0.01m, creditsCharged: 1),
            out _,
            clock: new FrozenClock(FixedClock.UtcNow.AddDays(8)),
            existingRepository: sharedRepository);

        var exception = await Assert.ThrowsAsync<WorkspaceSubscriptionInactiveException>(() =>
            expiredTrialService.EnsureAssistantUsageAllowedAsync(workspaceId));

        var latestSubscription = await sharedRepository.GetLatestSubscriptionAsync(workspaceId);

        Assert.Contains("active subscription", exception.Message, StringComparison.OrdinalIgnoreCase);
        Assert.NotNull(latestSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Expired, latestSubscription!.Status);
    }

    [Fact]
    public async Task GetCreditSummaryAsync_ReturnsDegradedSummary_WhenSubscriptionResolutionFails()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = new BillingPlan(
            Guid.NewGuid(),
            "starter",
            "Starter",
            49m,
            "USD",
            10_000,
            3,
            FixedClock.UtcNow);
        var repository = new InMemoryWorkspaceBillingRepository
        {
            ThrowOnSubscriptionRead = true
        };

        var service = CreateService(
            workspaceId,
            starterPlan,
            new StubUsagePricingCalculator(estimatedCost: 0.01m, creditsCharged: 1),
            out _,
            existingRepository: repository);

        var summary = await service.GetCreditSummaryAsync(new GetWorkspaceCreditSummaryQuery(workspaceId));

        Assert.True(summary.IsDegraded);
        Assert.Null(summary.BillingPlan);
        Assert.Null(summary.Subscription);
        Assert.Equal(5_000, summary.Balance.AvailableCredits);
    }

    private static WorkspaceBillingService CreateService(
        Guid workspaceId,
        BillingPlan starterPlan,
        IAiUsagePricingCalculator usagePricingCalculator,
        out InMemoryWorkspaceBillingRepository workspaceBillingRepository,
        long trialIncludedCredits = 5_000,
        IClock? clock = null,
        InMemoryWorkspaceBillingRepository? existingRepository = null)
    {
        workspaceBillingRepository = existingRepository ?? new InMemoryWorkspaceBillingRepository();
        var effectiveClock = clock ?? new FixedClock();

        return new WorkspaceBillingService(
            new AlwaysExistingWorkspaceRepository(workspaceId),
            new InMemoryBillingPlanRepository(starterPlan),
            workspaceBillingRepository,
            usagePricingCalculator,
            effectiveClock,
            new WorkspaceBillingOptions
            {
                DefaultPlanCode = starterPlan.Code,
                DefaultSubscriptionPeriodDays = 30,
                TrialPeriodDays = 7,
                TrialIncludedCredits = trialIncludedCredits,
                UsageMarkupMultiplier = 2.0m
            },
            NullLogger<WorkspaceBillingService>.Instance);
    }

    private sealed class AlwaysExistingWorkspaceRepository : IWorkspaceRepository
    {
        private readonly Guid _workspaceId;

        public AlwaysExistingWorkspaceRepository(Guid workspaceId)
        {
            _workspaceId = workspaceId;
        }

        public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(workspaceId == _workspaceId);
        }

        public Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Workspace?>(null);
        }
    }

    private sealed class InMemoryBillingPlanRepository : IBillingPlanRepository
    {
        private readonly BillingPlan _starterPlan;

        public InMemoryBillingPlanRepository(BillingPlan starterPlan)
        {
            _starterPlan = starterPlan;
        }

        public Task<BillingPlan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingPlan?>(
                string.Equals(code, _starterPlan.Code, StringComparison.OrdinalIgnoreCase)
                    ? _starterPlan
                    : null);
        }

        public Task<BillingPlan?> GetByIdAsync(Guid billingPlanId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingPlan?>(_starterPlan.Id == billingPlanId ? _starterPlan : null);
        }

        public Task<IReadOnlyList<BillingPlan>> ListAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingPlan>>([_starterPlan]);
        }

        public Task<IReadOnlyList<BillingPlan>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingPlan>>([_starterPlan]);
        }
    }

    private sealed class InMemoryWorkspaceBillingRepository : IWorkspaceBillingRepository
    {
        private readonly Dictionary<Guid, WorkspaceSubscription> _subscriptions = [];
        private readonly Dictionary<Guid, WorkspaceCreditBalance> _balances = [];
        private readonly Dictionary<Guid, WorkspaceBillingTransaction> _billingTransactions = [];
        private readonly Dictionary<string, WorkspaceBillingWebhookEvent> _webhookEvents = [];

        public List<WorkspaceCreditLedgerEntry> LedgerEntries { get; } = [];

        public List<WorkspaceUsageEvent> UsageEvents { get; } = [];

        public bool ThrowOnSubscriptionRead { get; set; }

        public Task EnsureProvisionedAsync(
            WorkspaceSubscription subscription,
            WorkspaceCreditBalance balance,
            WorkspaceCreditLedgerEntry? initialGrantEntry,
            CancellationToken cancellationToken = default)
        {
            if (!_subscriptions.ContainsKey(subscription.WorkspaceId))
            {
                _subscriptions[subscription.WorkspaceId] = subscription;
            }

            if (!_balances.ContainsKey(balance.WorkspaceId))
            {
                _balances[balance.WorkspaceId] = balance;
            }

            if (initialGrantEntry is null)
            {
                return Task.CompletedTask;
            }

            if (LedgerEntries.Any(entry =>
                    string.Equals(entry.ReferenceType, "workspace_subscription", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.ReferenceId, subscription.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
            {
                return Task.CompletedTask;
            }

            if (_subscriptions[subscription.WorkspaceId].Id == subscription.Id)
            {
                LedgerEntries.Add(initialGrantEntry);
                _balances[balance.WorkspaceId].GrantCredits(initialGrantEntry.Amount, initialGrantEntry.CreatedAtUtc);
            }

            return Task.CompletedTask;
        }

        public Task<WorkspaceSubscription?> GetCurrentSubscriptionAsync(Guid workspaceId, DateTime asOfUtc, CancellationToken cancellationToken = default)
        {
            if (ThrowOnSubscriptionRead)
            {
                throw new InvalidOperationException("Simulated subscription lookup failure.");
            }

            if (_subscriptions.TryGetValue(workspaceId, out var subscription) && subscription.IsActiveAt(asOfUtc))
            {
                return Task.FromResult<WorkspaceSubscription?>(subscription);
            }

            return Task.FromResult<WorkspaceSubscription?>(null);
        }

        public Task<WorkspaceSubscription?> GetLatestSubscriptionAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceSubscription?>(_subscriptions.GetValueOrDefault(workspaceId));
        }

        public Task<WorkspaceSubscription?> GetSubscriptionByProviderSubscriptionIdAsync(
            string provider,
            string providerSubscriptionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceSubscription?>(
                _subscriptions.Values.FirstOrDefault(subscription =>
                    string.Equals(subscription.Provider, provider, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(subscription.ProviderSubscriptionId, providerSubscriptionId, StringComparison.Ordinal)));
        }

        public Task<WorkspaceSubscription?> GetLatestSubscriptionByProviderCustomerIdAsync(
            string provider,
            string providerCustomerId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceSubscription?>(
                _subscriptions.Values
                    .Where(subscription =>
                        string.Equals(subscription.Provider, provider, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(subscription.ProviderCustomerId, providerCustomerId, StringComparison.Ordinal))
                    .OrderByDescending(subscription => subscription.UpdatedAtUtc)
                    .FirstOrDefault());
        }

        public Task<int> UpdateSubscriptionAsync(WorkspaceSubscription subscription, CancellationToken cancellationToken = default)
        {
            _subscriptions[subscription.WorkspaceId] = subscription;
            return Task.FromResult(1);
        }

        public Task<WorkspaceCreditBalance?> GetCreditBalanceAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceCreditBalance?>(_balances.GetValueOrDefault(workspaceId));
        }

        public Task<WorkspaceCreditBalance> AppendLedgerEntryAsync(WorkspaceCreditLedgerEntry entry, CancellationToken cancellationToken = default)
        {
            var balance = _balances[entry.WorkspaceId];
            if (entry.Amount > 0)
            {
                balance.GrantCredits(entry.Amount, entry.CreatedAtUtc);
            }
            else
            {
                balance.ConsumeCredits(-entry.Amount, entry.CreatedAtUtc);
            }

            LedgerEntries.Add(entry);
            return Task.FromResult(balance);
        }

        public Task<bool> LedgerReferenceExistsAsync(
            string referenceType,
            string referenceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult(
                LedgerEntries.Any(entry =>
                    string.Equals(entry.ReferenceType, referenceType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.ReferenceId, referenceId, StringComparison.Ordinal)));
        }

        public async Task<long> EnsureCreditGrantTargetAsync(
            Guid workspaceId,
            WorkspaceCreditLedgerType type,
            long targetAmount,
            string description,
            DateTime createdAtUtc,
            string referenceType,
            string referenceId,
            CancellationToken cancellationToken = default)
        {
            var currentAmount = LedgerEntries
                .Where(entry =>
                    entry.WorkspaceId == workspaceId
                    && string.Equals(entry.ReferenceType, referenceType, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.ReferenceId, referenceId, StringComparison.Ordinal))
                .Sum(entry => entry.Amount);
            var amountToGrant = Math.Max(0, targetAmount - currentAmount);

            if (amountToGrant <= 0)
            {
                return 0;
            }

            await AppendLedgerEntryAsync(
                new WorkspaceCreditLedgerEntry(
                    Guid.NewGuid(),
                    workspaceId,
                    type,
                    amountToGrant,
                    description,
                    createdAtUtc,
                    referenceType,
                    referenceId),
                cancellationToken);

            return amountToGrant;
        }

        public Task AddUsageEventAsync(WorkspaceUsageEvent usageEvent, CancellationToken cancellationToken = default)
        {
            UsageEvents.Add(usageEvent);
            return Task.CompletedTask;
        }

        public async Task<WorkspaceCreditBalance> RecordUsageAsync(
            WorkspaceUsageEvent usageEvent,
            WorkspaceCreditLedgerEntry debitEntry,
            CancellationToken cancellationToken = default)
        {
            var balance = await AppendLedgerEntryAsync(debitEntry, cancellationToken);
            UsageEvents.Add(usageEvent);
            return balance;
        }

        public Task<IReadOnlyList<WorkspaceCreditLedgerEntry>> ListLedgerEntriesAsync(Guid workspaceId, int take = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkspaceCreditLedgerEntry>>(
                LedgerEntries.Where(entry => entry.WorkspaceId == workspaceId).Take(take).ToArray());
        }

        public Task<IReadOnlyList<WorkspaceUsageEvent>> ListUsageEventsAsync(Guid workspaceId, int take = 100, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkspaceUsageEvent>>(
                UsageEvents.Where(entry => entry.WorkspaceId == workspaceId).Take(take).ToArray());
        }

        public Task AddBillingTransactionAsync(
            WorkspaceBillingTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            _billingTransactions[transaction.Id] = transaction;
            return Task.CompletedTask;
        }

        public Task<WorkspaceBillingTransaction?> GetBillingTransactionByCheckoutSessionIdAsync(
            string provider,
            string externalCheckoutSessionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceBillingTransaction?>(
                _billingTransactions.Values.FirstOrDefault(transaction =>
                    string.Equals(transaction.Provider, provider, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(transaction.ExternalCheckoutSessionId, externalCheckoutSessionId, StringComparison.Ordinal)));
        }

        public Task<WorkspaceBillingTransaction?> GetBillingTransactionByInvoiceIdAsync(
            string provider,
            string externalInvoiceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceBillingTransaction?>(
                _billingTransactions.Values.FirstOrDefault(transaction =>
                    string.Equals(transaction.Provider, provider, StringComparison.OrdinalIgnoreCase)
                    && string.Equals(transaction.ExternalInvoiceId, externalInvoiceId, StringComparison.Ordinal)));
        }

        public Task<WorkspaceBillingTransaction?> GetLatestBillingTransactionBySubscriptionIdAsync(
            string provider,
            string externalSubscriptionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceBillingTransaction?>(
                _billingTransactions.Values
                    .Where(transaction =>
                        string.Equals(transaction.Provider, provider, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(transaction.ExternalSubscriptionId, externalSubscriptionId, StringComparison.Ordinal))
                    .OrderByDescending(transaction => transaction.UpdatedAtUtc)
                    .FirstOrDefault());
        }

        public Task<WorkspaceBillingTransaction?> GetLatestPendingBillingTransactionByCustomerIdAsync(
            string provider,
            string externalCustomerId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceBillingTransaction?>(
                _billingTransactions.Values
                    .Where(transaction =>
                        string.Equals(transaction.Provider, provider, StringComparison.OrdinalIgnoreCase)
                        && string.Equals(transaction.ExternalCustomerId, externalCustomerId, StringComparison.Ordinal)
                        && transaction.Status == WorkspaceBillingTransactionStatus.Pending)
                    .OrderByDescending(transaction => transaction.UpdatedAtUtc)
                    .FirstOrDefault());
        }

        public Task UpdateBillingTransactionAsync(
            WorkspaceBillingTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            _billingTransactions[transaction.Id] = transaction;
            return Task.CompletedTask;
        }

        public Task<WorkspaceBillingWebhookEvent> UpsertBillingWebhookEventAsync(
            WorkspaceBillingWebhookEvent webhookEvent,
            CancellationToken cancellationToken = default)
        {
            var key = GetWebhookKey(webhookEvent.Provider, webhookEvent.ProviderEventId);
            if (_webhookEvents.TryGetValue(key, out var existing))
            {
                existing.RecordReceipt(webhookEvent.Payload, webhookEvent.SignatureHeader, webhookEvent.LastReceivedAtUtc);
                return Task.FromResult(existing);
            }

            _webhookEvents[key] = webhookEvent;
            return Task.FromResult(webhookEvent);
        }

        public Task<WorkspaceBillingWebhookEvent?> GetBillingWebhookEventByProviderEventIdAsync(
            string provider,
            string providerEventId,
            CancellationToken cancellationToken = default)
        {
            _webhookEvents.TryGetValue(GetWebhookKey(provider, providerEventId), out var webhookEvent);
            return Task.FromResult<WorkspaceBillingWebhookEvent?>(webhookEvent);
        }

        public Task<bool> TryStartBillingWebhookEventProcessingAsync(
            Guid webhookEventId,
            DateTime startedAtUtc,
            DateTime? staleProcessingBeforeUtc = null,
            CancellationToken cancellationToken = default)
        {
            var webhookEvent = _webhookEvents.Values.FirstOrDefault(entry => entry.Id == webhookEventId);
            if (webhookEvent is null)
            {
                return Task.FromResult(false);
            }

            if (webhookEvent.Status == WorkspaceBillingWebhookEventStatus.Processing &&
                staleProcessingBeforeUtc.HasValue &&
                webhookEvent.ProcessingStartedAtUtc <= staleProcessingBeforeUtc.Value)
            {
                webhookEvent.MarkFailed("Stale processing reclaimed for replay.", startedAtUtc);
            }

            return Task.FromResult(webhookEvent.TryMarkProcessing(startedAtUtc));
        }

        public Task MarkBillingWebhookEventProcessedAsync(
            Guid webhookEventId,
            DateTime processedAtUtc,
            CancellationToken cancellationToken = default)
        {
            _webhookEvents.Values.First(entry => entry.Id == webhookEventId).MarkProcessed(processedAtUtc);
            return Task.CompletedTask;
        }

        public Task MarkBillingWebhookEventFailedAsync(
            Guid webhookEventId,
            string error,
            DateTime failedAtUtc,
            CancellationToken cancellationToken = default)
        {
            _webhookEvents.Values.First(entry => entry.Id == webhookEventId).MarkFailed(error, failedAtUtc);
            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<WorkspaceBillingWebhookEvent>> ListReplayableBillingWebhookEventsAsync(
            string provider,
            int take,
            DateTime failedBeforeUtc,
            DateTime staleProcessingBeforeUtc,
            CancellationToken cancellationToken = default)
        {
            var results = _webhookEvents.Values
                .Where(entry =>
                    string.Equals(entry.Provider, provider, StringComparison.OrdinalIgnoreCase) &&
                    (entry.Status == WorkspaceBillingWebhookEventStatus.Pending
                     || (entry.Status == WorkspaceBillingWebhookEventStatus.Failed && entry.UpdatedAtUtc <= failedBeforeUtc)
                     || (entry.Status == WorkspaceBillingWebhookEventStatus.Processing
                         && entry.ProcessingStartedAtUtc <= staleProcessingBeforeUtc)))
                .OrderBy(entry => entry.LastReceivedAtUtc)
                .Take(Math.Max(1, take))
                .ToArray();

            return Task.FromResult<IReadOnlyList<WorkspaceBillingWebhookEvent>>(results);
        }

        private static string GetWebhookKey(string provider, string providerEventId)
        {
            return $"{provider.Trim().ToLowerInvariant()}::{providerEventId.Trim()}";
        }
    }

    private sealed class StubUsagePricingCalculator : IAiUsagePricingCalculator
    {
        private readonly AiUsageCharge _usageCharge;

        public StubUsagePricingCalculator(decimal estimatedCost, long creditsCharged)
        {
            _usageCharge = new AiUsageCharge(estimatedCost, estimatedCost * 2m, creditsCharged);
        }

        public AiUsageCharge Calculate(AiUsagePricingRequest request)
        {
            return _usageCharge;
        }
    }

    private sealed class FixedClock : IClock
    {
        public static DateTime UtcNow { get; } = new(2026, 4, 4, 12, 0, 0, DateTimeKind.Utc);

        DateTime IClock.UtcNow => UtcNow;
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
