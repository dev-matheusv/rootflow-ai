using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing.Commands;
using RootFlow.Application.Billing.Dtos;
using RootFlow.Domain.Billing;

namespace RootFlow.Application.Billing;

public sealed class WorkspacePaymentService
{
    private const string StripeProvider = "stripe";
    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IBillingPlanRepository _billingPlanRepository;
    private readonly IWorkspaceBillingRepository _workspaceBillingRepository;
    private readonly WorkspaceBillingService _workspaceBillingService;
    private readonly IStripePaymentGateway _stripePaymentGateway;
    private readonly IClock _clock;
    private readonly StripeBillingOptions _stripeOptions;
    private readonly ILogger<WorkspacePaymentService> _logger;

    public WorkspacePaymentService(
        IWorkspaceRepository workspaceRepository,
        IBillingPlanRepository billingPlanRepository,
        IWorkspaceBillingRepository workspaceBillingRepository,
        WorkspaceBillingService workspaceBillingService,
        IStripePaymentGateway stripePaymentGateway,
        IClock clock,
        StripeBillingOptions stripeOptions,
        ILogger<WorkspacePaymentService> logger)
    {
        _workspaceRepository = workspaceRepository;
        _billingPlanRepository = billingPlanRepository;
        _workspaceBillingRepository = workspaceBillingRepository;
        _workspaceBillingService = workspaceBillingService;
        _stripePaymentGateway = stripePaymentGateway;
        _clock = clock;
        _stripeOptions = stripeOptions;
        _logger = logger;
    }

    public Task<IReadOnlyList<BillingCreditPackDto>> ListCreditPacksAsync(
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<BillingCreditPackDto> creditPacks = _stripeOptions.CreditPacks
            .Select(pack => new BillingCreditPackDto(
                pack.Code,
                pack.Name,
                pack.Description,
                pack.Credits,
                decimal.Round(pack.Amount, 2, MidpointRounding.AwayFromZero),
                pack.CurrencyCode.Trim().ToUpperInvariant(),
                !string.IsNullOrWhiteSpace(pack.PriceId),
                string.IsNullOrWhiteSpace(pack.PriceId) ? null : pack.PriceId.Trim()))
            .ToArray();

        return Task.FromResult(creditPacks);
    }

    public async Task<BillingCheckoutSessionDto> CreateCheckoutAsync(
        CreateWorkspaceBillingCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        var priceId = command.PriceId?.Trim();
        if (string.IsNullOrWhiteSpace(priceId))
        {
            throw new ArgumentException("Price id is required.", nameof(command.PriceId));
        }

        var planCode = TryResolvePlanCodeByPriceId(priceId);
        if (!string.IsNullOrWhiteSpace(planCode))
        {
            return await CreateSubscriptionCheckoutInternalAsync(
                command.WorkspaceId,
                planCode,
                ResolveCheckoutSuccessUrl(),
                ResolveCheckoutCancelUrl(),
                cancellationToken);
        }

        var creditPack = ResolveCreditPackByPriceId(priceId);
        if (creditPack is not null)
        {
            return await CreateCreditPurchaseCheckoutInternalAsync(
                command.WorkspaceId,
                creditPack.Code,
                ResolveCheckoutSuccessUrl(),
                ResolveCheckoutCancelUrl(),
                cancellationToken);
        }

        throw new ArgumentException("The selected checkout price is not available.", nameof(command.PriceId));
    }

    public async Task<BillingCheckoutSessionDto> CreateSubscriptionCheckoutAsync(
        CreateWorkspaceSubscriptionCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        return await CreateSubscriptionCheckoutInternalAsync(
            command.WorkspaceId,
            command.PlanCode,
            ResolveCheckoutSuccessUrl(),
            ResolveCheckoutCancelUrl(),
            cancellationToken);
    }

    public async Task<BillingCheckoutSessionDto> CreateCreditPurchaseCheckoutAsync(
        CreateWorkspaceCreditPurchaseCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        return await CreateCreditPurchaseCheckoutInternalAsync(
            command.WorkspaceId,
            command.CreditPackCode,
            ResolveCheckoutSuccessUrl(),
            ResolveCheckoutCancelUrl(),
            cancellationToken);
    }

    private async Task<BillingCheckoutSessionDto> CreateSubscriptionCheckoutInternalAsync(
        Guid workspaceId,
        string planCode,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken)
    {
        await EnsureWorkspaceExistsAsync(workspaceId, cancellationToken);
        await _workspaceBillingService.EnsureTrialProvisionedAsync(workspaceId, cancellationToken);

        var plan = await _billingPlanRepository.GetByCodeAsync(planCode, cancellationToken);
        if (plan is null || !plan.IsActive)
        {
            throw new ArgumentException("The selected billing plan is not available.", nameof(planCode));
        }

        var priceId = ResolvePlanPriceId(plan.Code);
        var existingSubscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            workspaceId,
            cancellationToken);

        if (existingSubscription is not null &&
            existingSubscription.Status == WorkspaceSubscriptionStatus.Active &&
            existingSubscription.BillingPlanId == plan.Id)
        {
            throw new BillingCheckoutUnavailableException("This workspace is already on the selected plan.");
        }

        var now = _clock.UtcNow;
        var checkoutSession = await _stripePaymentGateway.CreateSubscriptionCheckoutSessionAsync(
            new StripeSubscriptionCheckoutRequest(
                workspaceId,
                plan.Code,
                priceId,
                successUrl,
                cancelUrl,
                existingSubscription?.Provider == StripeProvider
                    ? existingSubscription.ProviderCustomerId
                    : null),
            cancellationToken);

        var transaction = new WorkspaceBillingTransaction(
            Guid.NewGuid(),
            workspaceId,
            StripeProvider,
            WorkspaceBillingTransactionType.SubscriptionCheckout,
            WorkspaceBillingTransactionStatus.Pending,
            plan.MonthlyPrice,
            plan.CurrencyCode,
            now,
            now,
            billingPlanId: plan.Id,
            externalCheckoutSessionId: checkoutSession.SessionId,
            externalPaymentIntentId: checkoutSession.PaymentIntentId,
            externalSubscriptionId: checkoutSession.SubscriptionId,
            externalCustomerId: checkoutSession.CustomerId);

        await _workspaceBillingRepository.AddBillingTransactionAsync(transaction, cancellationToken);

        _logger.LogInformation(
            "Created Stripe subscription checkout session {CheckoutSessionId} for workspace {WorkspaceId} and plan {PlanCode}.",
            checkoutSession.SessionId,
            workspaceId,
            plan.Code);

        return new BillingCheckoutSessionDto(checkoutSession.SessionId, checkoutSession.Url);
    }

    private async Task<BillingCheckoutSessionDto> CreateCreditPurchaseCheckoutInternalAsync(
        Guid workspaceId,
        string creditPackCode,
        string successUrl,
        string cancelUrl,
        CancellationToken cancellationToken)
    {
        await EnsureWorkspaceExistsAsync(workspaceId, cancellationToken);
        await _workspaceBillingService.EnsureTrialProvisionedAsync(workspaceId, cancellationToken);

        var creditPack = ResolveCreditPack(creditPackCode);
        var existingSubscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            workspaceId,
            cancellationToken);
        var now = _clock.UtcNow;
        var checkoutSession = await _stripePaymentGateway.CreateCreditPurchaseCheckoutSessionAsync(
            new StripeCreditPurchaseCheckoutRequest(
                workspaceId,
                creditPack.Code,
                creditPack.Credits,
                creditPack.PriceId,
                successUrl,
                cancelUrl,
                existingSubscription?.Provider == StripeProvider
                    ? existingSubscription.ProviderCustomerId
                    : null),
            cancellationToken);

        var transaction = new WorkspaceBillingTransaction(
            Guid.NewGuid(),
            workspaceId,
            StripeProvider,
            WorkspaceBillingTransactionType.CreditPurchase,
            WorkspaceBillingTransactionStatus.Pending,
            creditPack.Amount,
            creditPack.CurrencyCode,
            now,
            now,
            creditAmount: creditPack.Credits,
            externalCheckoutSessionId: checkoutSession.SessionId,
            externalPaymentIntentId: checkoutSession.PaymentIntentId,
            externalCustomerId: checkoutSession.CustomerId);

        await _workspaceBillingRepository.AddBillingTransactionAsync(transaction, cancellationToken);

        _logger.LogInformation(
            "Created Stripe credit purchase checkout session {CheckoutSessionId} for workspace {WorkspaceId} and pack {CreditPackCode}.",
            checkoutSession.SessionId,
            workspaceId,
            creditPack.Code);

        return new BillingCheckoutSessionDto(checkoutSession.SessionId, checkoutSession.Url);
    }

    public async Task HandleStripeWebhookAsync(
        string payload,
        string signatureHeader,
        CancellationToken cancellationToken = default)
    {
        StripeWebhookEvent webhookEvent;
        try
        {
            webhookEvent = _stripePaymentGateway.ParseWebhook(payload, signatureHeader);
        }
        catch (BillingWebhookValidationException)
        {
            throw;
        }
        catch (Exception exception)
        {
            throw new BillingWebhookValidationException(exception.Message);
        }

        _logger.LogInformation(
            "Received Stripe webhook event {EventType} ({EventId}).",
            webhookEvent.EventType,
            webhookEvent.EventId);

        switch (webhookEvent)
        {
            case StripeCheckoutCompletedEvent checkoutCompletedEvent:
                await HandleCheckoutCompletedAsync(checkoutCompletedEvent, cancellationToken);
                break;
            case StripeInvoicePaidEvent invoicePaidEvent:
                await HandleInvoicePaidAsync(invoicePaidEvent, cancellationToken);
                break;
            case StripeSubscriptionUpdatedEvent subscriptionUpdatedEvent:
                await HandleSubscriptionUpdatedAsync(subscriptionUpdatedEvent, cancellationToken);
                break;
            case StripeUnhandledWebhookEvent unhandledWebhookEvent:
                _logger.LogDebug(
                    "Ignoring unsupported Stripe webhook event {EventType} ({EventId}).",
                    unhandledWebhookEvent.EventType,
                    unhandledWebhookEvent.EventId);
                break;
            default:
                _logger.LogDebug(
                    "Ignoring Stripe webhook event {EventType} ({EventId}) because no handler is registered.",
                    webhookEvent.EventType,
                    webhookEvent.EventId);
                break;
        }
    }

    private async Task HandleCheckoutCompletedAsync(
        StripeCheckoutCompletedEvent checkoutCompletedEvent,
        CancellationToken cancellationToken)
    {
        if (!string.Equals(checkoutCompletedEvent.PaymentStatus, "paid", StringComparison.OrdinalIgnoreCase) &&
            string.Equals(checkoutCompletedEvent.Mode, "payment", StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogInformation(
                "Skipping Stripe checkout session {CheckoutSessionId} because payment status is {PaymentStatus}.",
                checkoutCompletedEvent.SessionId,
                checkoutCompletedEvent.PaymentStatus);
            return;
        }

        var transaction = await _workspaceBillingRepository.GetBillingTransactionByCheckoutSessionIdAsync(
            StripeProvider,
            checkoutCompletedEvent.SessionId,
            cancellationToken);

        if (transaction is null)
        {
            _logger.LogWarning(
                "Stripe checkout session {CheckoutSessionId} could not be matched to a workspace billing transaction.",
                checkoutCompletedEvent.SessionId);
            return;
        }

        if (string.Equals(checkoutCompletedEvent.Mode, "subscription", StringComparison.OrdinalIgnoreCase))
        {
            transaction.MarkCompleted(
                checkoutCompletedEvent.OccurredAtUtc,
                externalCheckoutSessionId: checkoutCompletedEvent.SessionId,
                externalPaymentIntentId: checkoutCompletedEvent.PaymentIntentId,
                externalSubscriptionId: checkoutCompletedEvent.SubscriptionId,
                externalCustomerId: checkoutCompletedEvent.CustomerId);

            await _workspaceBillingRepository.UpdateBillingTransactionAsync(transaction, cancellationToken);

            _logger.LogInformation(
                "Marked Stripe subscription checkout {CheckoutSessionId} as completed for workspace {WorkspaceId}.",
                checkoutCompletedEvent.SessionId,
                transaction.WorkspaceId);

            if (string.IsNullOrWhiteSpace(checkoutCompletedEvent.SubscriptionId))
            {
                _logger.LogWarning(
                    "Stripe subscription checkout {CheckoutSessionId} for workspace {WorkspaceId} completed without a subscription id.",
                    checkoutCompletedEvent.SessionId,
                    transaction.WorkspaceId);
                return;
            }

            var stripeSubscription = await _stripePaymentGateway.GetSubscriptionAsync(
                checkoutCompletedEvent.SubscriptionId,
                cancellationToken);

            await SyncStripeSubscriptionAsync(
                stripeSubscription,
                checkoutCompletedEvent.EventType,
                checkoutCompletedEvent.OccurredAtUtc,
                fallbackWorkspaceId: checkoutCompletedEvent.WorkspaceId ?? transaction.WorkspaceId,
                fallbackBillingPlanId: transaction.BillingPlanId,
                cancellationToken);
            return;
        }

        if (transaction.IsCompleted)
        {
            _logger.LogInformation(
                "Stripe credit checkout {CheckoutSessionId} was already completed for workspace {WorkspaceId}.",
                checkoutCompletedEvent.SessionId,
                transaction.WorkspaceId);
            return;
        }

        if (await _workspaceBillingRepository.LedgerReferenceExistsAsync(
                "stripe_checkout_session",
                checkoutCompletedEvent.SessionId,
                cancellationToken))
        {
            transaction.MarkCompleted(
                checkoutCompletedEvent.OccurredAtUtc,
                externalCheckoutSessionId: checkoutCompletedEvent.SessionId,
                externalPaymentIntentId: checkoutCompletedEvent.PaymentIntentId,
                externalCustomerId: checkoutCompletedEvent.CustomerId);

            await _workspaceBillingRepository.UpdateBillingTransactionAsync(transaction, cancellationToken);
            return;
        }

        var creditsToGrant = transaction.CreditAmount.GetValueOrDefault();
        if (creditsToGrant <= 0)
        {
            _logger.LogWarning(
                "Stripe credit checkout {CheckoutSessionId} for workspace {WorkspaceId} did not contain a positive credit amount.",
                checkoutCompletedEvent.SessionId,
                transaction.WorkspaceId);
            return;
        }

        await _workspaceBillingService.GrantCreditsAsync(
            new GrantWorkspaceCreditsCommand(
                transaction.WorkspaceId,
                creditsToGrant,
                WorkspaceCreditLedgerType.CreditPurchase,
                $"Purchased credits via Stripe checkout {checkoutCompletedEvent.SessionId}",
                "stripe_checkout_session",
                checkoutCompletedEvent.SessionId),
            cancellationToken);

        transaction.MarkCompleted(
            checkoutCompletedEvent.OccurredAtUtc,
            externalCheckoutSessionId: checkoutCompletedEvent.SessionId,
            externalPaymentIntentId: checkoutCompletedEvent.PaymentIntentId,
            externalCustomerId: checkoutCompletedEvent.CustomerId);

        await _workspaceBillingRepository.UpdateBillingTransactionAsync(transaction, cancellationToken);

        _logger.LogInformation(
            "Granted {CreditsGranted} purchased credits to workspace {WorkspaceId} from Stripe checkout {CheckoutSessionId}.",
            creditsToGrant,
            transaction.WorkspaceId,
            checkoutCompletedEvent.SessionId);
    }

    private async Task HandleInvoicePaidAsync(
        StripeInvoicePaidEvent invoicePaidEvent,
        CancellationToken cancellationToken)
    {
        var existingInvoiceTransaction = await _workspaceBillingRepository.GetBillingTransactionByInvoiceIdAsync(
            StripeProvider,
            invoicePaidEvent.InvoiceId,
            cancellationToken);

        if (existingInvoiceTransaction?.IsCompleted == true)
        {
            _logger.LogInformation(
                "Ignoring duplicate Stripe invoice.paid event for invoice {InvoiceId}.",
                invoicePaidEvent.InvoiceId);
            return;
        }

        var subscription = await _workspaceBillingRepository.GetSubscriptionByProviderSubscriptionIdAsync(
            StripeProvider,
            invoicePaidEvent.SubscriptionId,
            cancellationToken);

        WorkspaceBillingTransaction? relatedCheckoutTransaction = null;
        StripeSubscriptionSyncContext? syncContext = null;
        if (subscription is null)
        {
            relatedCheckoutTransaction = await _workspaceBillingRepository.GetLatestBillingTransactionBySubscriptionIdAsync(
                StripeProvider,
                invoicePaidEvent.SubscriptionId,
                cancellationToken);

            var stripeSubscription = await _stripePaymentGateway.GetSubscriptionAsync(
                invoicePaidEvent.SubscriptionId,
                cancellationToken);

            syncContext = await SyncStripeSubscriptionAsync(
                stripeSubscription,
                invoicePaidEvent.EventType,
                invoicePaidEvent.OccurredAtUtc,
                fallbackWorkspaceId: relatedCheckoutTransaction?.WorkspaceId,
                fallbackBillingPlanId: relatedCheckoutTransaction?.BillingPlanId,
                cancellationToken);

            subscription = syncContext?.Subscription;

            if (subscription is null && relatedCheckoutTransaction is not null)
            {
                await _workspaceBillingService.EnsureTrialProvisionedAsync(
                    relatedCheckoutTransaction.WorkspaceId,
                    cancellationToken);

                subscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
                    relatedCheckoutTransaction.WorkspaceId,
                    cancellationToken);
            }
        }

        if (subscription is null)
        {
            _logger.LogWarning(
                "Stripe invoice {InvoiceId} could not resolve a billable workspace subscription after provisioning.",
                invoicePaidEvent.InvoiceId);
            return;
        }

        var plan = syncContext?.Plan ?? await ResolvePlanForStripeReferenceAsync(
            invoicePaidEvent.PriceId,
            null,
            subscription.BillingPlanId,
            relatedCheckoutTransaction?.BillingPlanId,
            cancellationToken);

        subscription.SyncProviderSubscription(
            plan.Id,
            WorkspaceSubscriptionStatus.Active,
            invoicePaidEvent.CurrentPeriodStartUtc,
            invoicePaidEvent.CurrentPeriodEndUtc,
            invoicePaidEvent.OccurredAtUtc,
            StripeProvider,
            invoicePaidEvent.CustomerId,
            invoicePaidEvent.SubscriptionId,
            invoicePaidEvent.PriceId);

        await _workspaceBillingRepository.UpdateSubscriptionAsync(subscription, cancellationToken);

        if (!await _workspaceBillingRepository.LedgerReferenceExistsAsync(
                "stripe_invoice",
                invoicePaidEvent.InvoiceId,
                cancellationToken))
        {
            await _workspaceBillingService.GrantCreditsAsync(
                new GrantWorkspaceCreditsCommand(
                    subscription.WorkspaceId,
                    plan.IncludedCredits,
                    WorkspaceCreditLedgerType.SubscriptionGrant,
                    $"Included credits for the {plan.Name} plan",
                    "stripe_invoice",
                    invoicePaidEvent.InvoiceId),
                cancellationToken);
        }

        var invoiceTransaction = existingInvoiceTransaction ?? new WorkspaceBillingTransaction(
            Guid.NewGuid(),
            subscription.WorkspaceId,
            StripeProvider,
            WorkspaceBillingTransactionType.SubscriptionInvoice,
            WorkspaceBillingTransactionStatus.Pending,
            invoicePaidEvent.AmountPaid,
            invoicePaidEvent.CurrencyCode,
            invoicePaidEvent.OccurredAtUtc,
            invoicePaidEvent.OccurredAtUtc,
            billingPlanId: plan.Id,
            externalSubscriptionId: invoicePaidEvent.SubscriptionId,
            externalInvoiceId: invoicePaidEvent.InvoiceId,
            externalCustomerId: invoicePaidEvent.CustomerId);

        invoiceTransaction.MarkCompleted(
            invoicePaidEvent.OccurredAtUtc,
            externalSubscriptionId: invoicePaidEvent.SubscriptionId,
            externalInvoiceId: invoicePaidEvent.InvoiceId,
            externalCustomerId: invoicePaidEvent.CustomerId);

        if (existingInvoiceTransaction is null)
        {
            await _workspaceBillingRepository.AddBillingTransactionAsync(invoiceTransaction, cancellationToken);
        }
        else
        {
            await _workspaceBillingRepository.UpdateBillingTransactionAsync(invoiceTransaction, cancellationToken);
        }

        _logger.LogInformation(
            "Applied Stripe invoice {InvoiceId} for workspace {WorkspaceId}, subscription {SubscriptionId}, plan {PlanCode}, price {PriceId}.",
            invoicePaidEvent.InvoiceId,
            subscription.WorkspaceId,
            invoicePaidEvent.SubscriptionId,
            plan.Code,
            invoicePaidEvent.PriceId);
    }

    private async Task HandleSubscriptionUpdatedAsync(
        StripeSubscriptionUpdatedEvent subscriptionUpdatedEvent,
        CancellationToken cancellationToken)
    {
        await SyncStripeSubscriptionAsync(
            new StripeSubscriptionSnapshot(
                subscriptionUpdatedEvent.SubscriptionId,
                subscriptionUpdatedEvent.WorkspaceId,
                subscriptionUpdatedEvent.PlanCode,
                subscriptionUpdatedEvent.CustomerId,
                subscriptionUpdatedEvent.PriceId,
                subscriptionUpdatedEvent.Status,
                subscriptionUpdatedEvent.CurrentPeriodStartUtc,
                subscriptionUpdatedEvent.CurrentPeriodEndUtc,
                subscriptionUpdatedEvent.CanceledAtUtc),
            subscriptionUpdatedEvent.EventType,
            subscriptionUpdatedEvent.OccurredAtUtc,
            fallbackWorkspaceId: subscriptionUpdatedEvent.WorkspaceId,
            fallbackBillingPlanId: null,
            cancellationToken);
    }

    private async Task EnsureWorkspaceExistsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspaceExists = await _workspaceRepository.ExistsAsync(workspaceId, cancellationToken);
        if (!workspaceExists)
        {
            throw new InvalidOperationException("Workspace was not found.");
        }
    }

    private async Task<BillingPlan> ResolvePlanForStripeReferenceAsync(
        string? priceId,
        string? planCode,
        Guid currentBillingPlanId,
        Guid? fallbackBillingPlanId,
        CancellationToken cancellationToken)
    {
        if (!string.IsNullOrWhiteSpace(planCode))
        {
            var planFromCode = await _billingPlanRepository.GetByCodeAsync(planCode, cancellationToken);
            if (planFromCode is not null)
            {
                return planFromCode;
            }
        }

        var resolvedPlanCode = TryResolvePlanCodeByPriceId(priceId);
        if (!string.IsNullOrWhiteSpace(resolvedPlanCode))
        {
            var mappedPlan = await _billingPlanRepository.GetByCodeAsync(resolvedPlanCode, cancellationToken);
            if (mappedPlan is not null)
            {
                return mappedPlan;
            }
        }

        var currentPlan = await _billingPlanRepository.GetByIdAsync(currentBillingPlanId, cancellationToken);
        if (currentPlan is not null)
        {
            return currentPlan;
        }

        if (fallbackBillingPlanId.HasValue)
        {
            var fallbackPlan = await _billingPlanRepository.GetByIdAsync(fallbackBillingPlanId.Value, cancellationToken);
            if (fallbackPlan is not null)
            {
                return fallbackPlan;
            }
        }

        throw new InvalidOperationException("The Stripe payment could not be matched to a configured billing plan.");
    }

    private async Task<StripeSubscriptionSyncContext?> SyncStripeSubscriptionAsync(
        StripeSubscriptionSnapshot stripeSubscription,
        string eventType,
        DateTime updatedAtUtc,
        Guid? fallbackWorkspaceId,
        Guid? fallbackBillingPlanId,
        CancellationToken cancellationToken)
    {
        var subscription = await _workspaceBillingRepository.GetSubscriptionByProviderSubscriptionIdAsync(
            StripeProvider,
            stripeSubscription.SubscriptionId,
            cancellationToken);
        var relatedTransaction = await _workspaceBillingRepository.GetLatestBillingTransactionBySubscriptionIdAsync(
            StripeProvider,
            stripeSubscription.SubscriptionId,
            cancellationToken);

        var resolvedWorkspaceId = subscription?.WorkspaceId
            ?? stripeSubscription.WorkspaceId
            ?? relatedTransaction?.WorkspaceId
            ?? fallbackWorkspaceId;

        if (!resolvedWorkspaceId.HasValue)
        {
            _logger.LogWarning(
                "Stripe subscription event {EventType} for {SubscriptionId} could not resolve a workspace. Price {PriceId}, plan code {PlanCode}.",
                eventType,
                stripeSubscription.SubscriptionId,
                stripeSubscription.PriceId,
                stripeSubscription.PlanCode);
            return null;
        }

        await _workspaceBillingService.EnsureTrialProvisionedAsync(resolvedWorkspaceId.Value, cancellationToken);

        subscription ??= await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            resolvedWorkspaceId.Value,
            cancellationToken);

        if (subscription is null)
        {
            _logger.LogWarning(
                "Stripe subscription event {EventType} for {SubscriptionId} resolved workspace {WorkspaceId} but no subscription row was available.",
                eventType,
                stripeSubscription.SubscriptionId,
                resolvedWorkspaceId.Value);
            return null;
        }

        var plan = await ResolvePlanForStripeReferenceAsync(
            stripeSubscription.PriceId,
            stripeSubscription.PlanCode,
            subscription.BillingPlanId,
            relatedTransaction?.BillingPlanId ?? fallbackBillingPlanId,
            cancellationToken);
        var status = MapStripeSubscriptionStatus(
            stripeSubscription.Status,
            stripeSubscription.CanceledAtUtc);

        subscription.SyncProviderSubscription(
            plan.Id,
            status,
            stripeSubscription.CurrentPeriodStartUtc,
            stripeSubscription.CurrentPeriodEndUtc,
            updatedAtUtc,
            StripeProvider,
            stripeSubscription.CustomerId,
            stripeSubscription.SubscriptionId,
            stripeSubscription.PriceId,
            stripeSubscription.CanceledAtUtc);

        await _workspaceBillingRepository.UpdateSubscriptionAsync(subscription, cancellationToken);

        _logger.LogInformation(
            "Persisted Stripe subscription {SubscriptionId} from event {EventType} for workspace {WorkspaceId}. Plan {PlanCode}, price {PriceId}, status {SubscriptionStatus}, period {PeriodStartUtc} to {PeriodEndUtc}.",
            stripeSubscription.SubscriptionId,
            eventType,
            subscription.WorkspaceId,
            plan.Code,
            stripeSubscription.PriceId,
            status,
            stripeSubscription.CurrentPeriodStartUtc,
            stripeSubscription.CurrentPeriodEndUtc);

        return new StripeSubscriptionSyncContext(subscription, plan, relatedTransaction);
    }

    private string ResolvePlanPriceId(string planCode)
    {
        var priceId = _stripeOptions.PlanPrices
            .FirstOrDefault(option => string.Equals(option.PlanCode, planCode, StringComparison.OrdinalIgnoreCase))
            ?.PriceId
            ?.Trim();

        if (string.IsNullOrWhiteSpace(priceId))
        {
            throw new BillingCheckoutUnavailableException("Checkout is not configured for the selected billing plan.");
        }

        return priceId;
    }

    private StripeCreditPackOptions ResolveCreditPack(string creditPackCode)
    {
        var creditPack = _stripeOptions.CreditPacks
            .FirstOrDefault(option => string.Equals(option.Code, creditPackCode, StringComparison.OrdinalIgnoreCase));

        if (creditPack is null)
        {
            throw new ArgumentException("The selected credit pack is not available.", nameof(creditPackCode));
        }

        if (string.IsNullOrWhiteSpace(creditPack.PriceId))
        {
            throw new BillingCheckoutUnavailableException("Checkout is not configured for the selected credit pack.");
        }

        return creditPack;
    }

    private StripeCreditPackOptions? ResolveCreditPackByPriceId(string priceId)
    {
        return _stripeOptions.CreditPacks.FirstOrDefault(option =>
            string.Equals(option.PriceId, priceId.Trim(), StringComparison.Ordinal));
    }

    private string? TryResolvePlanCodeByPriceId(string? priceId)
    {
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return null;
        }

        return _stripeOptions.PlanPrices
            .FirstOrDefault(option => string.Equals(option.PriceId, priceId.Trim(), StringComparison.Ordinal))
            ?.PlanCode;
    }

    private string ResolveCheckoutSuccessUrl()
    {
        var successUrl = _stripeOptions.CheckoutSuccessUrl?.Trim();
        if (string.IsNullOrWhiteSpace(successUrl))
        {
            throw new BillingCheckoutUnavailableException("Stripe checkout success URL is not configured.");
        }

        if (!successUrl.Contains(StripeBillingOptions.CheckoutSessionIdPlaceholder, StringComparison.Ordinal))
        {
            throw new BillingCheckoutUnavailableException(
                $"Stripe checkout success URL must include {StripeBillingOptions.CheckoutSessionIdPlaceholder}.");
        }

        return successUrl;
    }

    private string ResolveCheckoutCancelUrl()
    {
        var cancelUrl = _stripeOptions.CheckoutCancelUrl?.Trim();
        if (string.IsNullOrWhiteSpace(cancelUrl))
        {
            throw new BillingCheckoutUnavailableException("Stripe checkout cancel URL is not configured.");
        }

        return cancelUrl;
    }

    private static WorkspaceSubscriptionStatus MapStripeSubscriptionStatus(
        string stripeStatus,
        DateTime? canceledAtUtc)
    {
        var normalizedStatus = stripeStatus.Trim().ToLowerInvariant();
        return normalizedStatus switch
        {
            "active" => WorkspaceSubscriptionStatus.Active,
            "trialing" => WorkspaceSubscriptionStatus.Active,
            "canceled" => WorkspaceSubscriptionStatus.Canceled,
            "unpaid" => WorkspaceSubscriptionStatus.Expired,
            "incomplete_expired" => WorkspaceSubscriptionStatus.Expired,
            _ when canceledAtUtc.HasValue => WorkspaceSubscriptionStatus.Canceled,
            _ => WorkspaceSubscriptionStatus.Expired
        };
    }

    private sealed record StripeSubscriptionSyncContext(
        WorkspaceSubscription Subscription,
        BillingPlan Plan,
        WorkspaceBillingTransaction? RelatedTransaction);
}
