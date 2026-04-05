using Microsoft.Extensions.Logging.Abstractions;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing;
using RootFlow.Application.Billing.Commands;
using RootFlow.Domain.Billing;
using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Billing;

public sealed class WorkspacePaymentServiceTests
{
    [Fact]
    public async Task CreateSubscriptionCheckoutAsync_StoresPendingTransaction()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 50_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_123",
                "https://checkout.stripe.com/pay/cs_sub_123",
                "cus_123",
                "sub_123",
                null)
        };

        var service = CreateService(
            workspaceId,
            [starterPlan, proPlan],
            repository,
            gateway,
            out _);

        var checkoutSession = await service.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(workspaceId, "pro"));

        var transaction = await repository.GetBillingTransactionByCheckoutSessionIdAsync("stripe", "cs_sub_123");

        Assert.Equal("https://checkout.stripe.com/pay/cs_sub_123", checkoutSession.CheckoutUrl);
        Assert.NotNull(transaction);
        Assert.Equal(WorkspaceBillingTransactionType.SubscriptionCheckout, transaction!.Type);
        Assert.Equal(WorkspaceBillingTransactionStatus.Pending, transaction.Status);
        Assert.Equal(proPlan.Id, transaction.BillingPlanId);
        Assert.Equal("sub_123", transaction.ExternalSubscriptionId);
    }

    [Fact]
    public async Task CreateCheckoutAsync_WithPlanPriceId_UsesHostedUrls()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 50_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_hosted",
                "https://checkout.stripe.com/pay/cs_sub_hosted",
                "cus_hosted",
                "sub_hosted",
                null)
        };

        var service = CreateService(
            workspaceId,
            [starterPlan, proPlan],
            repository,
            gateway,
            out _);

        var checkoutSession = await service.CreateCheckoutAsync(
            new CreateWorkspaceBillingCheckoutCommand(workspaceId, "price_pro"));

        Assert.Equal("https://checkout.stripe.com/pay/cs_sub_hosted", checkoutSession.CheckoutUrl);
        Assert.NotNull(gateway.LastSubscriptionCheckoutRequest);
        Assert.Equal(
            "https://www.rootflow.com.br/billing?checkout=success&session_id={CHECKOUT_SESSION_ID}",
            gateway.LastSubscriptionCheckoutRequest!.SuccessUrl);
        Assert.Equal("https://www.rootflow.com.br/billing?checkout=cancel", gateway.LastSubscriptionCheckoutRequest.CancelUrl);
        Assert.Equal("price_pro", gateway.LastSubscriptionCheckoutRequest.PriceId);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_GrantsPurchasedCreditsOnlyOnce()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextCreditCheckout = new StripeCheckoutSessionResult(
                "cs_credits_123",
                "https://checkout.stripe.com/pay/cs_credits_123",
                "cus_credits",
                null,
                "pi_credits")
        };

        var service = CreateService(
            workspaceId,
            [starterPlan],
            repository,
            gateway,
            out var billingService,
            trialIncludedCredits: 100);

        await service.CreateCreditPurchaseCheckoutAsync(
            new CreateWorkspaceCreditPurchaseCheckoutCommand(workspaceId, "small"));

        gateway.NextWebhookEvent = new StripeCheckoutCompletedEvent(
            "evt_checkout_complete",
            FixedClock.CurrentUtcNow,
            "cs_credits_123",
            "payment",
            "paid",
            workspaceId,
            null,
            "small",
            10_000,
            "cus_credits",
            null,
            "pi_credits");

        await service.HandleStripeWebhookAsync("payload", "signature");
        await service.HandleStripeWebhookAsync("payload", "signature");

        var balance = await repository.GetCreditBalanceAsync(workspaceId);
        var transaction = await repository.GetBillingTransactionByCheckoutSessionIdAsync("stripe", "cs_credits_123");

        Assert.NotNull(balance);
        Assert.Equal(10_100, balance!.AvailableCredits);
        Assert.Equal(0, balance.ConsumedCredits);
        Assert.NotNull(transaction);
        Assert.True(transaction!.IsCompleted);
        Assert.Single(
            repository.LedgerEntries,
            entry => string.Equals(entry.ReferenceType, "stripe_checkout_session", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_AppliesInvoiceAndDoesNotDoubleGrantPlanCredits()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 50_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_456",
                "https://checkout.stripe.com/pay/cs_sub_456",
                "cus_pro",
                "sub_pro",
                null)
        };

        var service = CreateService(
            workspaceId,
            [starterPlan, proPlan],
            repository,
            gateway,
            out _,
            trialIncludedCredits: 100);

        await service.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(workspaceId, "pro"));

        gateway.NextWebhookEvent = new StripeCheckoutCompletedEvent(
            "evt_subscription_checkout",
            FixedClock.CurrentUtcNow,
            "cs_sub_456",
            "subscription",
            "paid",
            workspaceId,
            "pro",
            null,
            null,
            "cus_pro",
            "sub_pro",
            null);

        await service.HandleStripeWebhookAsync("payload", "signature");

        gateway.NextWebhookEvent = new StripeInvoicePaidEvent(
            "evt_invoice_paid",
            FixedClock.CurrentUtcNow,
            "in_123",
            "sub_pro",
            "cus_pro",
            "price_pro",
            99.90m,
            "BRL",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1));

        await service.HandleStripeWebhookAsync("payload", "signature");
        await service.HandleStripeWebhookAsync("payload", "signature");

        var latestSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);
        var balance = await repository.GetCreditBalanceAsync(workspaceId);
        var invoiceTransaction = await repository.GetBillingTransactionByInvoiceIdAsync("stripe", "in_123");

        Assert.NotNull(latestSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Active, latestSubscription!.Status);
        Assert.Equal(proPlan.Id, latestSubscription.BillingPlanId);
        Assert.Equal("sub_pro", latestSubscription.ProviderSubscriptionId);
        Assert.NotNull(balance);
        Assert.Equal(50_100, balance!.AvailableCredits);
        Assert.NotNull(invoiceTransaction);
        Assert.True(invoiceTransaction!.IsCompleted);
        Assert.Single(
            repository.LedgerEntries,
            entry =>
                string.Equals(entry.ReferenceType, "stripe_invoice", StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.ReferenceId, "in_123", StringComparison.Ordinal));
    }

    private static BillingPlan CreatePlan(
        string code,
        string name,
        decimal monthlyPrice,
        long includedCredits,
        int maxUsers)
    {
        return new BillingPlan(
            Guid.NewGuid(),
            code,
            name,
            monthlyPrice,
            "BRL",
            includedCredits,
            maxUsers,
            FixedClock.CurrentUtcNow);
    }

    private static WorkspacePaymentService CreateService(
        Guid workspaceId,
        IReadOnlyList<BillingPlan> plans,
        InMemoryWorkspaceBillingRepository repository,
        FakeStripePaymentGateway gateway,
        out WorkspaceBillingService workspaceBillingService,
        long trialIncludedCredits = 5_000)
    {
        var workspaceRepository = new AlwaysExistingWorkspaceRepository(workspaceId);
        var billingPlanRepository = new InMemoryBillingPlanRepository(plans);

        workspaceBillingService = new WorkspaceBillingService(
            workspaceRepository,
            billingPlanRepository,
            repository,
            new StubUsagePricingCalculator(),
            new FixedClock(),
            new WorkspaceBillingOptions
            {
                DefaultPlanCode = "starter",
                DefaultSubscriptionPeriodDays = 30,
                TrialPeriodDays = 7,
                TrialIncludedCredits = trialIncludedCredits,
                UsageMarkupMultiplier = 2.0m
            },
            NullLogger<WorkspaceBillingService>.Instance);

        return new WorkspacePaymentService(
            workspaceRepository,
            billingPlanRepository,
            repository,
            workspaceBillingService,
            gateway,
            new FixedClock(),
            new StripeBillingOptions
            {
                SecretKey = "sk_test_123",
                WebhookSecret = "whsec_123",
                CheckoutSuccessUrl = "https://www.rootflow.com.br/billing?checkout=success&session_id={CHECKOUT_SESSION_ID}",
                CheckoutCancelUrl = "https://www.rootflow.com.br/billing?checkout=cancel",
                PlanPrices =
                [
                    new StripePlanPriceOptions { PlanCode = "starter", PriceId = "price_starter" },
                    new StripePlanPriceOptions { PlanCode = "pro", PriceId = "price_pro" },
                    new StripePlanPriceOptions { PlanCode = "business", PriceId = "price_business" }
                ],
                CreditPacks =
                [
                    new StripeCreditPackOptions
                    {
                        Code = "small",
                        Name = "Small Credit Pack",
                        Description = "Extra shared credits for lighter workspace usage.",
                        Credits = 10_000,
                        Amount = 29.90m,
                        CurrencyCode = "BRL",
                        PriceId = "price_small"
                    },
                    new StripeCreditPackOptions
                    {
                        Code = "medium",
                        Name = "Medium Credit Pack",
                        Description = "Shared credits for teams that need more assistant continuity.",
                        Credits = 25_000,
                        Amount = 59.90m,
                        CurrencyCode = "BRL",
                        PriceId = "price_medium"
                    },
                    new StripeCreditPackOptions
                    {
                        Code = "large",
                        Name = "Large Credit Pack",
                        Description = "Larger shared credit reserve for higher workspace demand.",
                        Credits = 50_000,
                        Amount = 99.90m,
                        CurrencyCode = "BRL",
                        PriceId = "price_large"
                    }
                ]
            },
            NullLogger<WorkspacePaymentService>.Instance);
    }

    private sealed class FakeStripePaymentGateway : IStripePaymentGateway
    {
        public StripeCheckoutSessionResult? NextSubscriptionCheckout { get; set; }

        public StripeCheckoutSessionResult? NextCreditCheckout { get; set; }

        public StripeWebhookEvent? NextWebhookEvent { get; set; }

        public StripeSubscriptionCheckoutRequest? LastSubscriptionCheckoutRequest { get; private set; }

        public StripeCreditPurchaseCheckoutRequest? LastCreditCheckoutRequest { get; private set; }

        public Task<StripeCheckoutSessionResult> CreateSubscriptionCheckoutSessionAsync(
            StripeSubscriptionCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            LastSubscriptionCheckoutRequest = request;
            return Task.FromResult(NextSubscriptionCheckout ?? throw new InvalidOperationException("Subscription checkout was not configured."));
        }

        public Task<StripeCheckoutSessionResult> CreateCreditPurchaseCheckoutSessionAsync(
            StripeCreditPurchaseCheckoutRequest request,
            CancellationToken cancellationToken = default)
        {
            LastCreditCheckoutRequest = request;
            return Task.FromResult(NextCreditCheckout ?? throw new InvalidOperationException("Credit checkout was not configured."));
        }

        public StripeWebhookEvent ParseWebhook(string payload, string signatureHeader)
        {
            return NextWebhookEvent ?? throw new InvalidOperationException("Webhook event was not configured.");
        }
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
        private readonly IReadOnlyList<BillingPlan> _plans;

        public InMemoryBillingPlanRepository(IReadOnlyList<BillingPlan> plans)
        {
            _plans = plans;
        }

        public Task<BillingPlan?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingPlan?>(
                _plans.FirstOrDefault(plan => string.Equals(plan.Code, code, StringComparison.OrdinalIgnoreCase)));
        }

        public Task<BillingPlan?> GetByIdAsync(Guid billingPlanId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<BillingPlan?>(_plans.FirstOrDefault(plan => plan.Id == billingPlanId));
        }

        public Task<IReadOnlyList<BillingPlan>> ListAllAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult(_plans);
        }

        public Task<IReadOnlyList<BillingPlan>> ListActiveAsync(CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<BillingPlan>>(_plans.Where(plan => plan.IsActive).ToArray());
        }
    }

    private sealed class InMemoryWorkspaceBillingRepository : IWorkspaceBillingRepository
    {
        private readonly Dictionary<Guid, WorkspaceSubscription> _subscriptions = [];
        private readonly Dictionary<Guid, WorkspaceCreditBalance> _balances = [];
        private readonly Dictionary<Guid, WorkspaceBillingTransaction> _billingTransactions = [];

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

            if (initialGrantEntry is null || LedgerEntries.Any(entry =>
                    string.Equals(entry.ReferenceType, "workspace_subscription", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.ReferenceId, subscription.Id.ToString(), StringComparison.Ordinal)))
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

        public Task<WorkspaceSubscription?> GetCurrentSubscriptionAsync(
            Guid workspaceId,
            DateTime asOfUtc,
            CancellationToken cancellationToken = default)
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

        public Task UpdateSubscriptionAsync(WorkspaceSubscription subscription, CancellationToken cancellationToken = default)
        {
            _subscriptions[subscription.WorkspaceId] = subscription;
            return Task.CompletedTask;
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

        public Task<IReadOnlyList<WorkspaceCreditLedgerEntry>> ListLedgerEntriesAsync(
            Guid workspaceId,
            int take = 100,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkspaceCreditLedgerEntry>>(
                LedgerEntries.Where(entry => entry.WorkspaceId == workspaceId).Take(take).ToArray());
        }

        public Task<IReadOnlyList<WorkspaceUsageEvent>> ListUsageEventsAsync(
            Guid workspaceId,
            int take = 100,
            CancellationToken cancellationToken = default)
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

        public Task UpdateBillingTransactionAsync(
            WorkspaceBillingTransaction transaction,
            CancellationToken cancellationToken = default)
        {
            _billingTransactions[transaction.Id] = transaction;
            return Task.CompletedTask;
        }
    }

    private sealed class StubUsagePricingCalculator : IAiUsagePricingCalculator
    {
        public AiUsageCharge Calculate(AiUsagePricingRequest request)
        {
            return new AiUsageCharge(0m, 0m, 0);
        }
    }

    private sealed class FixedClock : IClock
    {
        public static DateTime CurrentUtcNow { get; } = new(2026, 4, 4, 12, 0, 0, DateTimeKind.Utc);

        public DateTime UtcNow => CurrentUtcNow;
    }
}
