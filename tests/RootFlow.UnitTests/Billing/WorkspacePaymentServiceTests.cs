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
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
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
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
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
            out _,
            trialIncludedCredits: 100);

        repository.SeedSubscription(CreateActiveStripeSubscription(
            workspaceId,
            starterPlan,
            customerId: "cus_credits",
            subscriptionId: "sub_paid_credits",
            priceId: "price_starter"));

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
        Assert.Equal(5_000, balance!.AvailableCredits);
        Assert.Equal(0, balance.ConsumedCredits);
        Assert.NotNull(transaction);
        Assert.True(transaction!.IsCompleted);
        Assert.Single(
            repository.LedgerEntries,
            entry => string.Equals(entry.ReferenceType, "stripe_checkout_session", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_SendsCreditPurchaseConfirmationOnlyOnce()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextCreditCheckout = new StripeCheckoutSessionResult(
                "cs_credits_email",
                "https://checkout.stripe.com/pay/cs_credits_email",
                "cus_credits_email",
                null,
                "pi_credits_email")
        };

        var service = CreateService(
            workspaceId,
            [starterPlan],
            repository,
            gateway,
            out _,
            out var billingNotifier,
            trialIncludedCredits: 100);

        repository.SeedSubscription(CreateActiveStripeSubscription(
            workspaceId,
            starterPlan,
            customerId: "cus_credits_email",
            subscriptionId: "sub_paid_credits_email",
            priceId: "price_starter"));

        await service.CreateCreditPurchaseCheckoutAsync(
            new CreateWorkspaceCreditPurchaseCheckoutCommand(workspaceId, "small"));

        gateway.NextWebhookEvent = new StripeCheckoutCompletedEvent(
            "evt_credit_email",
            FixedClock.CurrentUtcNow,
            "cs_credits_email",
            "payment",
            "paid",
            workspaceId,
            null,
            "small",
            10_000,
            "cus_credits_email",
            null,
            "pi_credits_email");

        await service.HandleStripeWebhookAsync("payload", "signature");
        await service.HandleStripeWebhookAsync("payload", "signature");

        var notification = Assert.Single(billingNotifier.Notifications);
        Assert.Equal(WorkspacePaymentConfirmationKind.CreditPurchase, notification.Kind);
        Assert.Equal("Small Credit Pack", notification.ItemName);
        Assert.Equal(5_000, notification.CreditsGranted);
    }

    [Fact]
    public async Task CreateCreditPurchaseCheckoutAsync_Throws_WhenWorkspaceIsTrialing()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway();

        var service = CreateService(
            workspaceId,
            [starterPlan],
            repository,
            gateway,
            out _,
            trialIncludedCredits: 100);

        var exception = await Assert.ThrowsAsync<BillingCheckoutUnavailableException>(() =>
            service.CreateCreditPurchaseCheckoutAsync(
                new CreateWorkspaceCreditPurchaseCheckoutCommand(workspaceId, "small")));

        Assert.Contains("paid plan", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task ReplayPendingStripeWebhooksAsync_ReprocessesFailedSubscriptionCheckout()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_replay",
                "https://checkout.stripe.com/pay/cs_sub_replay",
                "cus_replay",
                null,
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
            "evt_subscription_replay",
            FixedClock.CurrentUtcNow,
            "cs_sub_replay",
            "subscription",
            "paid",
            workspaceId,
            "pro",
            null,
            null,
            "cus_replay",
            "sub_replay",
            null);

        await Assert.ThrowsAsync<BillingWebhookProcessingException>(() =>
            service.HandleStripeWebhookAsync("payload", "signature"));

        var failedWebhookEvent = Assert.Single(repository.WebhookEvents);
        Assert.Equal(WorkspaceBillingWebhookEventStatus.Failed, failedWebhookEvent.Status);

        gateway.SubscriptionSnapshots["sub_replay"] = new StripeSubscriptionSnapshot(
            "sub_replay",
            workspaceId,
            "pro",
            "cus_replay",
            "price_pro",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null);

        var replayedCount = await service.ReplayPendingStripeWebhooksAsync();

        var latestSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);
        var persistedWebhookEvent = Assert.Single(repository.WebhookEvents);

        Assert.Equal(1, replayedCount);
        Assert.NotNull(latestSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Active, latestSubscription!.Status);
        Assert.Equal(proPlan.Id, latestSubscription.BillingPlanId);
        Assert.Equal(WorkspaceBillingWebhookEventStatus.Processed, persistedWebhookEvent.Status);
        Assert.Equal(2, persistedWebhookEvent.AttemptCount);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_AppliesInvoiceAndDoesNotDoubleGrantPlanCredits()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
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

        gateway.SubscriptionSnapshots["sub_pro"] = new StripeSubscriptionSnapshot(
            "sub_pro",
            workspaceId,
            "pro",
            "cus_pro",
            "price_pro",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null);

        var service = CreateService(
            workspaceId,
            [starterPlan, proPlan],
            repository,
            gateway,
            out _,
            out var billingNotifier,
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
        Assert.Equal(25_100, balance!.AvailableCredits);
        Assert.NotNull(invoiceTransaction);
        Assert.True(invoiceTransaction!.IsCompleted);
        Assert.Single(
            repository.LedgerEntries,
            entry =>
                string.Equals(entry.ReferenceType, "stripe_subscription_period", StringComparison.OrdinalIgnoreCase)
                && string.Equals(entry.ReferenceId, FixedClock.CurrentUtcNow.Ticks.ToString(), StringComparison.Ordinal));
        var notification = Assert.Single(billingNotifier.Notifications);
        Assert.Equal(WorkspacePaymentConfirmationKind.Subscription, notification.Kind);
        Assert.Equal("Pro", notification.ItemName);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_UpgradeCheckoutTopUpsIncludedCreditsWithoutDoubleGrantingInvoice()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
        var businessPlan = CreatePlan("business", "Business", 199.90m, 50_000, 50);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_pro",
                "https://checkout.stripe.com/pay/cs_sub_pro",
                "cus_upgrade",
                "sub_pro",
                null)
        };

        gateway.SubscriptionSnapshots["sub_pro"] = new StripeSubscriptionSnapshot(
            "sub_pro",
            workspaceId,
            "pro",
            "cus_upgrade",
            "price_pro",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null);

        var service = CreateService(
            workspaceId,
            [starterPlan, proPlan, businessPlan],
            repository,
            gateway,
            out _,
            trialIncludedCredits: 100);

        await service.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(workspaceId, "pro"));

        gateway.NextWebhookEvent = new StripeCheckoutCompletedEvent(
            "evt_subscription_checkout_pro",
            FixedClock.CurrentUtcNow,
            "cs_sub_pro",
            "subscription",
            "paid",
            workspaceId,
            "pro",
            null,
            null,
            "cus_upgrade",
            "sub_pro",
            null);

        await service.HandleStripeWebhookAsync("payload", "signature");

        gateway.NextSubscriptionCheckout = new StripeCheckoutSessionResult(
            "cs_sub_business",
            "https://checkout.stripe.com/pay/cs_sub_business",
            "cus_upgrade",
            "sub_business",
            null);
        gateway.SubscriptionSnapshots["sub_business"] = new StripeSubscriptionSnapshot(
            "sub_business",
            workspaceId,
            "business",
            "cus_upgrade",
            "price_business",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null);

        await service.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(workspaceId, "business"));

        gateway.NextWebhookEvent = new StripeCheckoutCompletedEvent(
            "evt_subscription_checkout_business",
            FixedClock.CurrentUtcNow,
            "cs_sub_business",
            "subscription",
            "paid",
            workspaceId,
            "business",
            null,
            null,
            "cus_upgrade",
            "sub_business",
            null);

        await service.HandleStripeWebhookAsync("payload", "signature");

        gateway.NextWebhookEvent = new StripeInvoicePaidEvent(
            "evt_invoice_business",
            FixedClock.CurrentUtcNow,
            "in_business",
            "sub_business",
            "cus_upgrade",
            "price_business",
            199.90m,
            "BRL",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1));

        await service.HandleStripeWebhookAsync("payload", "signature");

        var latestSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);
        var balance = await repository.GetCreditBalanceAsync(workspaceId);

        Assert.NotNull(latestSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Active, latestSubscription!.Status);
        Assert.Equal(businessPlan.Id, latestSubscription.BillingPlanId);
        Assert.NotNull(balance);
        Assert.Equal(50_100, balance!.AvailableCredits);
        Assert.Equal(
            50_000,
            repository.LedgerEntries
                .Where(entry =>
                    entry.WorkspaceId == workspaceId
                    && string.Equals(entry.ReferenceType, "stripe_subscription_period", StringComparison.OrdinalIgnoreCase)
                    && string.Equals(entry.ReferenceId, FixedClock.CurrentUtcNow.Ticks.ToString(), StringComparison.Ordinal))
                .Sum(entry => entry.Amount));
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_SyncsPaidSubscriptionFromCheckoutCompleted()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_sync",
                "https://checkout.stripe.com/pay/cs_sub_sync",
                "cus_sync",
                null,
                null)
        };

        gateway.SubscriptionSnapshots["sub_sync"] = new StripeSubscriptionSnapshot(
            "sub_sync",
            workspaceId,
            "pro",
            "cus_sync",
            "price_pro",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null);

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
            "evt_subscription_checkout_sync",
            FixedClock.CurrentUtcNow,
            "cs_sub_sync",
            "subscription",
            "paid",
            workspaceId,
            "pro",
            null,
            null,
            "cus_sync",
            "sub_sync",
            null);

        await service.HandleStripeWebhookAsync("payload", "signature");

        var latestSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);

        Assert.NotNull(latestSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Active, latestSubscription!.Status);
        Assert.Equal(proPlan.Id, latestSubscription.BillingPlanId);
        Assert.Equal("stripe", latestSubscription.Provider);
        Assert.Equal("sub_sync", latestSubscription.ProviderSubscriptionId);
        Assert.Null(latestSubscription.TrialEndsAtUtc);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_SyncsSubscriptionCreatedFromMetadataWhenCheckoutTransactionHasNoSubscriptionId()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_created",
                "https://checkout.stripe.com/pay/cs_sub_created",
                null,
                null,
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

        gateway.NextWebhookEvent = new StripeSubscriptionUpdatedEvent(
            "evt_subscription_created",
            "customer.subscription.created",
            FixedClock.CurrentUtcNow,
            "sub_created",
            "cus_created",
            "price_pro",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null,
            workspaceId,
            "pro");

        await service.HandleStripeWebhookAsync("payload", "signature");

        var latestSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);

        Assert.NotNull(latestSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Active, latestSubscription!.Status);
        Assert.Equal(proPlan.Id, latestSubscription.BillingPlanId);
        Assert.Equal("sub_created", latestSubscription.ProviderSubscriptionId);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_InvoicePaidFetchesStripeSubscriptionWhenCorrelationIsMissing()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_invoice_fallback",
                "https://checkout.stripe.com/pay/cs_sub_invoice_fallback",
                null,
                null,
                null)
        };

        gateway.SubscriptionSnapshots["sub_invoice_fallback"] = new StripeSubscriptionSnapshot(
            "sub_invoice_fallback",
            workspaceId,
            "pro",
            "cus_invoice_fallback",
            "price_pro",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null);

        var service = CreateService(
            workspaceId,
            [starterPlan, proPlan],
            repository,
            gateway,
            out _,
            trialIncludedCredits: 100);

        await service.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(workspaceId, "pro"));

        gateway.NextWebhookEvent = new StripeInvoicePaidEvent(
            "evt_invoice_paid_fallback",
            FixedClock.CurrentUtcNow,
            "in_fallback",
            "sub_invoice_fallback",
            "cus_invoice_fallback",
            "price_pro",
            99.90m,
            "BRL",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1));

        await service.HandleStripeWebhookAsync("payload", "signature");

        var latestSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);
        var balance = await repository.GetCreditBalanceAsync(workspaceId);

        Assert.NotNull(latestSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Active, latestSubscription!.Status);
        Assert.Equal(proPlan.Id, latestSubscription.BillingPlanId);
        Assert.Equal("sub_invoice_fallback", latestSubscription.ProviderSubscriptionId);
        Assert.NotNull(balance);
        Assert.Equal(25_100, balance!.AvailableCredits);
    }

    [Fact]
    public async Task HandleStripeWebhookAsync_InvoicePaid_OverridesCurrentTrialRow_WhenCustomerMatchesHistoricalStripeSubscription()
    {
        var workspaceId = Guid.NewGuid();
        var starterPlan = CreatePlan("starter", "Starter", 49.90m, 10_000, 3);
        var proPlan = CreatePlan("pro", "Pro", 99.90m, 25_000, 10);
        var repository = new InMemoryWorkspaceBillingRepository();
        var gateway = new FakeStripePaymentGateway
        {
            NextSubscriptionCheckout = new StripeCheckoutSessionResult(
                "cs_sub_trial_override",
                "https://checkout.stripe.com/pay/cs_sub_trial_override",
                "cus_trial_override",
                null,
                null)
        };

        gateway.SubscriptionSnapshots["sub_trial_override"] = new StripeSubscriptionSnapshot(
            "sub_trial_override",
            workspaceId,
            "pro",
            "cus_trial_override",
            "price_pro",
            "active",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            null);

        var service = CreateService(
            workspaceId,
            [starterPlan, proPlan],
            repository,
            gateway,
            out _,
            trialIncludedCredits: 100);

        await service.CreateSubscriptionCheckoutAsync(
            new CreateWorkspaceSubscriptionCheckoutCommand(workspaceId, "pro"));

        var currentTrialSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);
        Assert.NotNull(currentTrialSubscription);
        Assert.Equal(WorkspaceSubscriptionStatus.Trial, currentTrialSubscription!.Status);

        var historicalStripeSubscription = new WorkspaceSubscription(
            Guid.NewGuid(),
            workspaceId,
            starterPlan.Id,
            WorkspaceSubscriptionStatus.Canceled,
            FixedClock.CurrentUtcNow.AddMonths(-2),
            FixedClock.CurrentUtcNow.AddMonths(-1),
            FixedClock.CurrentUtcNow.AddMonths(-2),
            FixedClock.CurrentUtcNow.AddDays(-10),
            canceledAtUtc: FixedClock.CurrentUtcNow.AddMonths(-1),
            provider: "stripe",
            providerCustomerId: "cus_trial_override",
            providerSubscriptionId: "sub_old_trial_override",
            providerPriceId: "price_starter");

        repository.SeedSubscription(historicalStripeSubscription);

        gateway.NextWebhookEvent = new StripeInvoicePaidEvent(
            "evt_invoice_paid_trial_override",
            FixedClock.CurrentUtcNow,
            "in_trial_override",
            "sub_trial_override",
            "cus_trial_override",
            "price_pro",
            99.90m,
            "BRL",
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1));

        await service.HandleStripeWebhookAsync("payload", "signature");

        var latestSubscription = await repository.GetLatestSubscriptionAsync(workspaceId);
        var historicalSubscriptionAfterSync = repository.Subscriptions.Single(subscription => subscription.Id == historicalStripeSubscription.Id);

        Assert.NotNull(latestSubscription);
        Assert.Equal(currentTrialSubscription.Id, latestSubscription!.Id);
        Assert.Equal(WorkspaceSubscriptionStatus.Active, latestSubscription.Status);
        Assert.Equal(proPlan.Id, latestSubscription.BillingPlanId);
        Assert.Equal("stripe", latestSubscription.Provider);
        Assert.Equal("cus_trial_override", latestSubscription.ProviderCustomerId);
        Assert.Equal("sub_trial_override", latestSubscription.ProviderSubscriptionId);
        Assert.Null(latestSubscription.TrialEndsAtUtc);
        Assert.Equal(WorkspaceSubscriptionStatus.Canceled, historicalSubscriptionAfterSync.Status);
        Assert.Equal("sub_old_trial_override", historicalSubscriptionAfterSync.ProviderSubscriptionId);
    }

    private static WorkspaceSubscription CreateActiveStripeSubscription(
        Guid workspaceId,
        BillingPlan plan,
        string customerId,
        string subscriptionId,
        string priceId)
    {
        return new WorkspaceSubscription(
            Guid.NewGuid(),
            workspaceId,
            plan.Id,
            WorkspaceSubscriptionStatus.Active,
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow.AddMonths(1),
            FixedClock.CurrentUtcNow,
            FixedClock.CurrentUtcNow,
            provider: "stripe",
            providerCustomerId: customerId,
            providerSubscriptionId: subscriptionId,
            providerPriceId: priceId);
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
        long trialIncludedCredits = 3_000)
    {
        return CreateService(
            workspaceId,
            plans,
            repository,
            gateway,
            out workspaceBillingService,
            out _,
            trialIncludedCredits);
    }

    private static WorkspacePaymentService CreateService(
        Guid workspaceId,
        IReadOnlyList<BillingPlan> plans,
        InMemoryWorkspaceBillingRepository repository,
        FakeStripePaymentGateway gateway,
        out WorkspaceBillingService workspaceBillingService,
        out FakeWorkspaceBillingNotifier billingNotifier,
        long trialIncludedCredits = 3_000)
    {
        var workspaceRepository = new AlwaysExistingWorkspaceRepository(workspaceId);
        var workspaceMembershipRepository = new InMemoryWorkspaceMembershipRepository();
        var billingPlanRepository = new InMemoryBillingPlanRepository(plans);
        billingNotifier = new FakeWorkspaceBillingNotifier();

        workspaceMembershipRepository.AddMember(
            workspaceId,
            "Owner Person",
            "owner@rootflow.test",
            WorkspaceRole.Owner);

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
            workspaceMembershipRepository,
            billingPlanRepository,
            repository,
            workspaceBillingService,
            billingNotifier,
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
                        Credits = 5_000,
                        Amount = 29.90m,
                        CurrencyCode = "BRL",
                        PriceId = "price_small"
                    },
                    new StripeCreditPackOptions
                    {
                        Code = "medium",
                        Name = "Medium Credit Pack",
                        Description = "Shared credits for teams that need more assistant continuity.",
                        Credits = 10_000,
                        Amount = 59.90m,
                        CurrencyCode = "BRL",
                        PriceId = "price_medium"
                    },
                    new StripeCreditPackOptions
                    {
                        Code = "large",
                        Name = "Large Credit Pack",
                        Description = "Larger shared credit reserve for higher workspace demand.",
                        Credits = 20_000,
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

        public Dictionary<string, StripeSubscriptionSnapshot> SubscriptionSnapshots { get; } = [];

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

        public Task<StripeSubscriptionSnapshot> GetSubscriptionAsync(
            string subscriptionId,
            CancellationToken cancellationToken = default)
        {
            if (SubscriptionSnapshots.TryGetValue(subscriptionId, out var subscriptionSnapshot))
            {
                return Task.FromResult(subscriptionSnapshot);
            }

            throw new InvalidOperationException("Stripe subscription snapshot was not configured.");
        }

        public StripeWebhookEvent ParseWebhook(string payload, string signatureHeader)
        {
            return NextWebhookEvent ?? throw new InvalidOperationException("Webhook event was not configured.");
        }
    }

    private sealed class FakeWorkspaceBillingNotifier : IWorkspaceBillingNotifier
    {
        public List<WorkspacePaymentConfirmationNotification> Notifications { get; } = [];

        public List<WorkspaceBillingLifecycleNotification> LifecycleNotifications { get; } = [];

        public List<PlatformBillingAlertNotification> PlatformAlerts { get; } = [];

        public Task SendPaymentConfirmedAsync(
            WorkspacePaymentConfirmationNotification notification,
            CancellationToken cancellationToken = default)
        {
            Notifications.Add(notification);
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

    private sealed class AlwaysExistingWorkspaceRepository : IWorkspaceRepository
    {
        private readonly Workspace _workspace;

        public AlwaysExistingWorkspaceRepository(Guid workspaceId)
        {
            _workspace = new Workspace(
                workspaceId,
                "RootFlow Workspace",
                "rootflow-workspace",
                FixedClock.CurrentUtcNow);
        }

        public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(workspaceId == _workspace.Id);
        }

        public Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<Workspace?>(workspaceId == _workspace.Id ? _workspace : null);
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
        private readonly Dictionary<string, WorkspaceBillingWebhookEvent> _webhookEvents = [];
        private readonly HashSet<string> _notificationDeliveries = new(StringComparer.OrdinalIgnoreCase);

        public List<WorkspaceCreditLedgerEntry> LedgerEntries { get; } = [];

        public List<WorkspaceUsageEvent> UsageEvents { get; } = [];

        public IReadOnlyList<WorkspaceBillingWebhookEvent> WebhookEvents => _webhookEvents.Values.ToArray();

        public IReadOnlyList<WorkspaceSubscription> Subscriptions =>
            _subscriptions.Values
                .OrderBy(subscription => subscription.WorkspaceId)
                .ThenBy(subscription => subscription.CreatedAtUtc)
                .ThenBy(subscription => subscription.Id)
                .ToArray();

        public void SeedSubscription(WorkspaceSubscription subscription)
        {
            _subscriptions[subscription.Id] = subscription;
        }

        public Task EnsureProvisionedAsync(
            WorkspaceSubscription subscription,
            WorkspaceCreditBalance balance,
            WorkspaceCreditLedgerEntry? initialGrantEntry,
            CancellationToken cancellationToken = default)
        {
            if (!_subscriptions.Values.Any(existing => existing.WorkspaceId == subscription.WorkspaceId))
            {
                _subscriptions[subscription.Id] = subscription;
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

            if (_subscriptions.TryGetValue(subscription.Id, out var currentSubscription) &&
                currentSubscription.WorkspaceId == subscription.WorkspaceId)
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
            return Task.FromResult<WorkspaceSubscription?>(
                _subscriptions.Values
                    .Where(subscription =>
                        subscription.WorkspaceId == workspaceId
                        && subscription.CurrentPeriodStartUtc <= asOfUtc
                        && subscription.IsActiveAt(asOfUtc))
                    .OrderBy(subscription => subscription.Status switch
                    {
                        WorkspaceSubscriptionStatus.Active => 0,
                        WorkspaceSubscriptionStatus.Trial => 1,
                        _ => 2
                    })
                    .ThenByDescending(subscription => subscription.CurrentPeriodEndUtc)
                    .ThenByDescending(subscription => subscription.UpdatedAtUtc)
                    .ThenByDescending(subscription => subscription.CreatedAtUtc)
                    .FirstOrDefault());
        }

        public Task<WorkspaceSubscription?> GetLatestSubscriptionAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceSubscription?>(
                _subscriptions.Values
                    .Where(subscription => subscription.WorkspaceId == workspaceId)
                    .OrderBy(subscription => subscription.Status switch
                    {
                        WorkspaceSubscriptionStatus.Active => 0,
                        WorkspaceSubscriptionStatus.Trial => 1,
                        WorkspaceSubscriptionStatus.Canceled => 2,
                        _ => 3
                    })
                    .ThenByDescending(subscription => subscription.UpdatedAtUtc)
                    .ThenByDescending(subscription => subscription.CurrentPeriodEndUtc)
                    .ThenByDescending(subscription => subscription.CreatedAtUtc)
                    .FirstOrDefault());
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
                    .OrderBy(subscription => subscription.Status switch
                    {
                        WorkspaceSubscriptionStatus.Active => 0,
                        WorkspaceSubscriptionStatus.Trial => 1,
                        WorkspaceSubscriptionStatus.Canceled => 2,
                        _ => 3
                    })
                    .ThenByDescending(subscription => subscription.UpdatedAtUtc)
                    .ThenByDescending(subscription => subscription.CurrentPeriodEndUtc)
                    .ThenByDescending(subscription => subscription.CreatedAtUtc)
                    .FirstOrDefault());
        }

        public Task<int> UpdateSubscriptionAsync(WorkspaceSubscription subscription, CancellationToken cancellationToken = default)
        {
            if (!_subscriptions.ContainsKey(subscription.Id))
            {
                return Task.FromResult(0);
            }

            _subscriptions[subscription.Id] = subscription;
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

        public Task<WorkspaceBillingTransaction?> GetBillingTransactionByIdAsync(
            Guid transactionId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceBillingTransaction?>(
                _billingTransactions.GetValueOrDefault(transactionId));
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

    private sealed class InMemoryWorkspaceMembershipRepository : IWorkspaceMembershipRepository
    {
        private readonly Dictionary<Guid, List<WorkspaceMemberRecord>> _memberships = [];

        public void AddMember(Guid workspaceId, string fullName, string email, WorkspaceRole role)
        {
            if (!_memberships.TryGetValue(workspaceId, out var members))
            {
                members = [];
                _memberships[workspaceId] = members;
            }

            members.Add(new WorkspaceMemberRecord(
                Guid.NewGuid(),
                fullName,
                email,
                role,
                FixedClock.CurrentUtcNow,
                true));
        }

        public Task<WorkspaceMembership?> GetAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<WorkspaceMembership?>(null);
        }

        public Task AddAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task UpdateAsync(WorkspaceMembership membership, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<IReadOnlyList<WorkspaceMemberRecord>> ListByWorkspaceAsync(
            Guid workspaceId,
            CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<WorkspaceMemberRecord>>(
                _memberships.TryGetValue(workspaceId, out var members)
                    ? members.ToArray()
                    : []);
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
