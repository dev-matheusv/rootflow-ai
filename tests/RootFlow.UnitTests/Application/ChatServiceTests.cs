using Microsoft.Extensions.Logging.Abstractions;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing;
using RootFlow.Application.Chat;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Conversations.Dtos;
using RootFlow.Domain.Billing;
using RootFlow.Domain.Conversations;
using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Application;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task AskAsync_HonorsRequestedMaxContextChunks_WhenSearching()
    {
        var searchService = new RecordingKnowledgeSearchService(_ => Array.Empty<KnowledgeSearchMatch>());
        var chatService = CreateChatService(
            searchService,
            new RecordingChatCompletionService(),
            out _,
            out _);

        await chatService.AskAsync(
            new AskQuestionCommand(Guid.NewGuid(), "What is the travel policy?", MaxContextChunks: 3));

        Assert.Equal(3, searchService.CapturedMaxResults);
    }

    [Fact]
    public async Task AskAsync_ReturnsProfessionalFallback_WhenEvidenceIsWeak()
    {
        var weakMatch = new KnowledgeSearchMatch(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "travel-policy.md",
            "Travel Policy",
            "Travel Policy:\nFlights longer than six hours may be booked in premium economy with manager approval.",
            0,
            0.19d,
            0.27d,
            0d,
            0d,
            Array.Empty<string>());

        var searchService = new RecordingKnowledgeSearchService(_ => [weakMatch]);
        var completionService = new RecordingChatCompletionService();
        var chatService = CreateChatService(
            searchService,
            completionService,
            out _,
            out var workspaceBillingRepository);

        var response = await chatService.AskAsync(
            new AskQuestionCommand(Guid.NewGuid(), "What hotline should I use for a security incident?", MaxContextChunks: 3));

        Assert.Contains("limited evidence", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Single(response.Sources);
        Assert.Equal("travel-policy.md", response.Sources[0].DocumentName);
        Assert.Equal(0, completionService.CallCount);
        Assert.Empty(workspaceBillingRepository.UsageEvents);
    }

    [Fact]
    public async Task AskAsync_Blocks_WhenWorkspaceHasNoCredits()
    {
        var searchService = new RecordingKnowledgeSearchService(_ => Array.Empty<KnowledgeSearchMatch>());
        var completionService = new RecordingChatCompletionService();
        var chatService = CreateChatService(
            searchService,
            completionService,
            out var conversationRepository,
            out _,
            includedCredits: 0,
            minimumAssistantCreditsRequired: 1);

        var exception = await Assert.ThrowsAsync<InsufficientWorkspaceCreditsException>(() =>
            chatService.AskAsync(new AskQuestionCommand(Guid.NewGuid(), "What is the travel policy?")));

        Assert.Equal("Your workspace has no credits available.", exception.Message);
        Assert.Equal(0, completionService.CallCount);
        Assert.Equal(0, conversationRepository.ConversationCount);
        Assert.Equal(0, conversationRepository.TotalMessageCount);
    }

    [Fact]
    public async Task AskAsync_RegistersUsageAndDebitsWorkspaceCredits_WhenCompletionRuns()
    {
        var strongMatch = new KnowledgeSearchMatch(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "travel-policy.md",
            "Travel Policy",
            "Travel Policy:\nThe travel policy requires approval for premium bookings and lists hotline procedures.",
            0,
            0.63d,
            0.52d,
            0.16d,
            0.12d,
            ["travel", "policy", "approval"]);

        var workspaceId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var searchService = new RecordingKnowledgeSearchService(_ => [strongMatch]);
        var completionService = new RecordingChatCompletionService(
            usage: new ChatCompletionUsage(400, 200, 600));
        var chatService = CreateChatService(
            searchService,
            completionService,
            out _,
            out var workspaceBillingRepository,
            usagePricingCalculator: new StubUsagePricingCalculator(estimatedCost: 0.12m, creditsCharged: 12));

        var response = await chatService.AskAsync(
            new AskQuestionCommand(workspaceId, "What does the travel policy say about premium bookings?", UserId: userId));

        var balance = await workspaceBillingRepository.GetCreditBalanceAsync(workspaceId);

        Assert.Equal(1, completionService.CallCount);
        Assert.NotNull(balance);
        Assert.Equal(88, balance!.AvailableCredits);
        Assert.Equal(12, balance.ConsumedCredits);
        Assert.Single(workspaceBillingRepository.UsageEvents);
        Assert.Equal(userId, workspaceBillingRepository.UsageEvents[0].UserId);
        Assert.Equal(response.ConversationId, workspaceBillingRepository.UsageEvents[0].ConversationId);
        Assert.Equal("test", workspaceBillingRepository.UsageEvents[0].Provider);
        Assert.Equal("test-model", workspaceBillingRepository.UsageEvents[0].Model);
        Assert.Equal(600, workspaceBillingRepository.UsageEvents[0].TotalTokens);
        Assert.Equal(2, workspaceBillingRepository.LedgerEntries.Count);
        Assert.Equal(WorkspaceCreditLedgerType.UsageDebit, workspaceBillingRepository.LedgerEntries[^1].Type);
    }

    private static ChatService CreateChatService(
        RecordingKnowledgeSearchService searchService,
        RecordingChatCompletionService completionService,
        out InMemoryConversationRepository conversationRepository,
        out InMemoryWorkspaceBillingRepository workspaceBillingRepository,
        long includedCredits = 100,
        long minimumAssistantCreditsRequired = 1,
        IAiUsagePricingCalculator? usagePricingCalculator = null)
    {
        conversationRepository = new InMemoryConversationRepository();
        workspaceBillingRepository = new InMemoryWorkspaceBillingRepository();
        var billingPlan = new BillingPlan(
            Guid.NewGuid(),
            "starter",
            "Starter",
            49m,
            "USD",
            includedCredits,
            3,
            FixedClock.CurrentUtcNow);
        var billingService = new WorkspaceBillingService(
            new AlwaysExistingWorkspaceRepository(),
            new InMemoryBillingPlanRepository(billingPlan),
            workspaceBillingRepository,
            usagePricingCalculator ?? new StubUsagePricingCalculator(estimatedCost: 0.01m, creditsCharged: 1),
            new FixedClock(),
            new WorkspaceBillingOptions
            {
                DefaultPlanCode = billingPlan.Code,
                DefaultSubscriptionPeriodDays = 30,
                TrialPeriodDays = 7,
                TrialIncludedCredits = includedCredits,
                MinimumAssistantCreditsRequired = minimumAssistantCreditsRequired,
                UsageMarkupMultiplier = 2.0m
            },
            NullLogger<WorkspaceBillingService>.Instance);

        return new ChatService(
            conversationRepository,
            new StubEmbeddingService(),
            searchService,
            completionService,
            billingService,
            new FixedClock(),
            NullLogger<ChatService>.Instance);
    }

    private sealed class AlwaysExistingWorkspaceRepository : IWorkspaceRepository
    {
        public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) => Task.FromResult<Workspace?>(null);
    }

    private sealed class InMemoryConversationRepository : IConversationRepository
    {
        private readonly Dictionary<Guid, Conversation> _conversations = [];
        private readonly Dictionary<Guid, List<ConversationMessage>> _messages = [];

        public int ConversationCount => _conversations.Count;

        public int TotalMessageCount => _messages.Values.Sum(static messages => messages.Count);

        public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            _conversations[conversation.Id] = conversation;
            _messages[conversation.Id] = [];
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            _conversations[conversation.Id] = conversation;
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
        {
            if (!_messages.TryGetValue(message.ConversationId, out var messages))
            {
                messages = [];
                _messages[message.ConversationId] = messages;
            }

            messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<Conversation?> GetByIdAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation)
                && conversation.WorkspaceId == workspaceId)
            {
                return Task.FromResult<Conversation?>(conversation);
            }

            return Task.FromResult<Conversation?>(null);
        }

        public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation)
                && conversation.WorkspaceId == workspaceId
                && _messages.TryGetValue(conversationId, out var messages))
            {
                return Task.FromResult<IReadOnlyList<ConversationMessage>>(messages.ToArray());
            }

            return Task.FromResult<IReadOnlyList<ConversationMessage>>(Array.Empty<ConversationMessage>());
        }

        public Task<IReadOnlyList<ConversationSummaryDto>> ListSummariesAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(Array.Empty<ConversationSummaryDto>());
        }
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { 0.42f, 0.58f });
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new[] { 0.42f, 0.58f }).ToArray());
        }
    }

    private sealed class RecordingKnowledgeSearchService : IKnowledgeSearchService
    {
        private readonly Func<string, IReadOnlyList<KnowledgeSearchMatch>> _resultsFactory;

        public RecordingKnowledgeSearchService(Func<string, IReadOnlyList<KnowledgeSearchMatch>> resultsFactory)
        {
            _resultsFactory = resultsFactory;
        }

        public int CapturedMaxResults { get; private set; }

        public Task<IReadOnlyList<KnowledgeSearchMatch>> SearchAsync(
            Guid workspaceId,
            string queryText,
            float[] queryEmbedding,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            CapturedMaxResults = maxResults;
            return Task.FromResult(_resultsFactory(queryText));
        }
    }

    private sealed class RecordingChatCompletionService : IChatCompletionService
    {
        private readonly ChatCompletionUsage _usage;

        public RecordingChatCompletionService(ChatCompletionUsage? usage = null)
        {
            _usage = usage ?? new ChatCompletionUsage(120, 80, 200);
        }

        public int CallCount { get; private set; }

        public Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatCompletionResponse("Structured answer. [1]", "test-model", "test", _usage));
        }
    }

    private sealed class FixedClock : IClock
    {
        public static DateTime CurrentUtcNow { get; } = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => CurrentUtcNow;
    }

    private sealed class InMemoryBillingPlanRepository : IBillingPlanRepository
    {
        private readonly BillingPlan _billingPlan;

        public InMemoryBillingPlanRepository(BillingPlan billingPlan)
        {
            _billingPlan = billingPlan;
        }

        public Task<BillingPlan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingPlan?>(
                string.Equals(code, _billingPlan.Code, StringComparison.OrdinalIgnoreCase)
                    ? _billingPlan
                    : null);
        }

        public Task<BillingPlan?> GetByIdAsync(Guid billingPlanId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingPlan?>(_billingPlan.Id == billingPlanId ? _billingPlan : null);
        }

        public Task<IReadOnlyList<BillingPlan>> ListAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingPlan>>([_billingPlan]);
        }

        public Task<IReadOnlyList<BillingPlan>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingPlan>>([_billingPlan]);
        }
    }

    private sealed class InMemoryWorkspaceBillingRepository : IWorkspaceBillingRepository
    {
        private readonly Dictionary<Guid, WorkspaceSubscription> _subscriptions = [];
        private readonly Dictionary<Guid, WorkspaceCreditBalance> _balances = [];
        private readonly Dictionary<Guid, WorkspaceBillingTransaction> _billingTransactions = [];
        private readonly Dictionary<string, WorkspaceBillingWebhookEvent> _webhookEvents = [];
        private readonly HashSet<string> _notificationDeliveries = new(StringComparer.OrdinalIgnoreCase);

        public List<WorkspaceCreditLedgerEntry> LedgerEntries { get; } = [];

        public List<WorkspaceUsageEvent> UsageEvents { get; } = [];

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

        private static string GetWebhookKey(string provider, string providerEventId)
        {
            return $"{provider.Trim().ToLowerInvariant()}::{providerEventId.Trim()}";
        }

        private static string BuildDeliveryKey(string notificationKind, string dedupeKey, string recipientEmail)
        {
            return $"{notificationKind.Trim()}::{dedupeKey.Trim()}::{recipientEmail.Trim().ToUpperInvariant()}";
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
}
