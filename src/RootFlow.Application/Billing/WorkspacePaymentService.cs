using Microsoft.Extensions.Logging;
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
    private readonly IWorkspaceMembershipRepository _workspaceMembershipRepository;
    private readonly IBillingPlanRepository _billingPlanRepository;
    private readonly IWorkspaceBillingRepository _workspaceBillingRepository;
    private readonly WorkspaceBillingService _workspaceBillingService;
    private readonly IWorkspaceBillingNotifier _workspaceBillingNotifier;
    private readonly IStripePaymentGateway _stripePaymentGateway;
    private readonly IClock _clock;
    private readonly StripeBillingOptions _stripeOptions;
    private readonly ILogger<WorkspacePaymentService> _logger;

    public WorkspacePaymentService(
        IWorkspaceRepository workspaceRepository,
        IWorkspaceMembershipRepository workspaceMembershipRepository,
        IBillingPlanRepository billingPlanRepository,
        IWorkspaceBillingRepository workspaceBillingRepository,
        WorkspaceBillingService workspaceBillingService,
        IWorkspaceBillingNotifier workspaceBillingNotifier,
        IStripePaymentGateway stripePaymentGateway,
        IClock clock,
        StripeBillingOptions stripeOptions,
        ILogger<WorkspacePaymentService> logger)
    {
        _workspaceRepository = workspaceRepository;
        _workspaceMembershipRepository = workspaceMembershipRepository;
        _billingPlanRepository = billingPlanRepository;
        _workspaceBillingRepository = workspaceBillingRepository;
        _workspaceBillingService = workspaceBillingService;
        _workspaceBillingNotifier = workspaceBillingNotifier;
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
            "Created Stripe subscription checkout session {CheckoutSessionId} for workspace {WorkspaceId}, plan {PlanCode}, customer {CustomerId}, subscription {SubscriptionId}.",
            checkoutSession.SessionId,
            workspaceId,
            plan.Code,
            checkoutSession.CustomerId,
            checkoutSession.SubscriptionId);

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
            "Created Stripe credit purchase checkout session {CheckoutSessionId} for workspace {WorkspaceId}, pack {CreditPackCode}, customer {CustomerId}.",
            checkoutSession.SessionId,
            workspaceId,
            creditPack.Code,
            checkoutSession.CustomerId);

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

        LogWebhookReceipt(webhookEvent);

        try
        {
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
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Stripe webhook event {EventType} ({EventId}) failed during RootFlow billing synchronization.",
                webhookEvent.EventType,
                webhookEvent.EventId);
            throw new BillingWebhookProcessingException(
                $"Stripe webhook event {webhookEvent.EventType} failed during billing synchronization.",
                exception);
        }
    }

    private async Task HandleCheckoutCompletedAsync(
        StripeCheckoutCompletedEvent checkoutCompletedEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing Stripe checkout.session.completed {EventId} for session {CheckoutSessionId}, mode {CheckoutMode}, workspace {WorkspaceId}, customer {CustomerId}, subscription {SubscriptionId}.",
            checkoutCompletedEvent.EventId,
            checkoutCompletedEvent.SessionId,
            checkoutCompletedEvent.Mode,
            checkoutCompletedEvent.WorkspaceId,
            checkoutCompletedEvent.CustomerId,
            checkoutCompletedEvent.SubscriptionId);

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

        var creditPack = ResolveCreditPackOrDefault(checkoutCompletedEvent.CreditPackCode);

        await TrySendPaymentConfirmedEmailAsync(
            transaction.WorkspaceId,
            WorkspacePaymentConfirmationKind.CreditPurchase,
            creditPack.Name,
            transaction.Amount,
            transaction.CurrencyCode,
            $"A compra de {creditPack.Name} foi confirmada e os creditos ja estao disponiveis para o workspace.",
            creditPack.Credits,
            cancellationToken);

        _logger.LogInformation(
            "Granted {CreditsGranted} purchased credits to workspace {WorkspaceId} from Stripe checkout {CheckoutSessionId}. Customer {CustomerId}.",
            creditsToGrant,
            transaction.WorkspaceId,
            checkoutCompletedEvent.SessionId,
            checkoutCompletedEvent.CustomerId);
    }

    private async Task HandleInvoicePaidAsync(
        StripeInvoicePaidEvent invoicePaidEvent,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation(
            "Processing Stripe invoice.paid for invoice {InvoiceId}, subscription {SubscriptionId}, customer {CustomerId}.",
            invoicePaidEvent.InvoiceId,
            invoicePaidEvent.SubscriptionId,
            invoicePaidEvent.CustomerId);

        var existingInvoiceTransaction = await _workspaceBillingRepository.GetBillingTransactionByInvoiceIdAsync(
            StripeProvider,
            invoicePaidEvent.InvoiceId,
            cancellationToken);

        var stripeSubscription = await _stripePaymentGateway.GetSubscriptionAsync(
            invoicePaidEvent.SubscriptionId,
            cancellationToken);

        _logger.LogInformation(
            "Fetched Stripe subscription {SubscriptionId} for invoice {InvoiceId}. Customer {CustomerId}, price {PriceId}, status {SubscriptionStatus}.",
            stripeSubscription.SubscriptionId,
            invoicePaidEvent.InvoiceId,
            stripeSubscription.CustomerId,
            stripeSubscription.PriceId,
            stripeSubscription.Status);

        if (existingInvoiceTransaction?.IsCompleted == true)
        {
            _logger.LogInformation(
                "Ignoring duplicate Stripe invoice.paid event for invoice {InvoiceId}.",
                invoicePaidEvent.InvoiceId);
            return;
        }

        var syncContext = await SyncStripeSubscriptionAsync(
            stripeSubscription,
            invoicePaidEvent.EventType,
            invoicePaidEvent.OccurredAtUtc,
            fallbackWorkspaceId: stripeSubscription.WorkspaceId,
            fallbackBillingPlanId: null,
            cancellationToken);

        var subscription = syncContext?.Subscription;

        if (subscription is null)
        {
            _logger.LogWarning(
                "Stripe invoice {InvoiceId} could not resolve a billable workspace subscription after Stripe fetch and local matching.",
                invoicePaidEvent.InvoiceId);
            return;
        }

        var plan = syncContext!.Plan;

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
            externalSubscriptionId: stripeSubscription.SubscriptionId,
            externalInvoiceId: invoicePaidEvent.InvoiceId,
            externalCustomerId: stripeSubscription.CustomerId);

        invoiceTransaction.MarkCompleted(
            invoicePaidEvent.OccurredAtUtc,
            externalSubscriptionId: stripeSubscription.SubscriptionId,
            externalInvoiceId: invoicePaidEvent.InvoiceId,
            externalCustomerId: stripeSubscription.CustomerId);

        if (existingInvoiceTransaction is null)
        {
            await _workspaceBillingRepository.AddBillingTransactionAsync(invoiceTransaction, cancellationToken);
        }
        else
        {
            await _workspaceBillingRepository.UpdateBillingTransactionAsync(invoiceTransaction, cancellationToken);
        }

        await TrySendPaymentConfirmedEmailAsync(
            subscription.WorkspaceId,
            WorkspacePaymentConfirmationKind.Subscription,
            plan.Name,
            invoicePaidEvent.AmountPaid,
            invoicePaidEvent.CurrencyCode,
            $"O plano {plan.Name} foi confirmado e esta ativo para o workspace.",
            null,
            cancellationToken);

        _logger.LogInformation(
            "Applied Stripe invoice {InvoiceId} for workspace {WorkspaceId}, subscription {SubscriptionId}, plan {PlanCode}, price {PriceId}.",
            invoicePaidEvent.InvoiceId,
            subscription.WorkspaceId,
            stripeSubscription.SubscriptionId,
            plan.Code,
            stripeSubscription.PriceId);
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
        _logger.LogInformation(
            "Resolving internal billing plan for Stripe reference. Price {PriceId}, plan code {PlanCode}, current billing plan id {CurrentBillingPlanId}, fallback billing plan id {FallbackBillingPlanId}.",
            priceId,
            planCode,
            currentBillingPlanId,
            fallbackBillingPlanId);

        if (!string.IsNullOrWhiteSpace(planCode))
        {
            var planFromCode = await _billingPlanRepository.GetByCodeAsync(planCode, cancellationToken);
            if (planFromCode is not null)
            {
                _logger.LogInformation(
                    "Resolved internal billing plan {BillingPlanId} ({PlanCode}) directly from Stripe plan code {StripePlanCode}.",
                    planFromCode.Id,
                    planFromCode.Code,
                    planCode);
                return planFromCode;
            }

            _logger.LogError(
                "Stripe billing sync could not resolve plan code {PlanCode} to an internal billing plan.",
                planCode);
        }

        var resolvedPlanCode = TryResolvePlanCodeByPriceId(priceId);
        if (!string.IsNullOrWhiteSpace(resolvedPlanCode))
        {
            var mappedPlan = await _billingPlanRepository.GetByCodeAsync(resolvedPlanCode, cancellationToken);
            if (mappedPlan is not null)
            {
                _logger.LogInformation(
                    "Resolved internal billing plan {BillingPlanId} ({PlanCode}) from Stripe price {PriceId}.",
                    mappedPlan.Id,
                    mappedPlan.Code,
                    priceId);
                return mappedPlan;
            }

            _logger.LogError(
                "Stripe billing sync resolved price {PriceId} to plan code {PlanCode}, but that plan is missing in RootFlow.",
                priceId,
                resolvedPlanCode);
        }
        else if (!string.IsNullOrWhiteSpace(priceId))
        {
            _logger.LogError(
                "Stripe billing sync could not map Stripe price {PriceId} to a configured RootFlow billing plan.",
                priceId);
        }

        if (fallbackBillingPlanId.HasValue)
        {
            var fallbackPlan = await _billingPlanRepository.GetByIdAsync(fallbackBillingPlanId.Value, cancellationToken);
            if (fallbackPlan is not null)
            {
                _logger.LogWarning(
                    "Falling back to billing plan {BillingPlanId} ({PlanCode}) for Stripe price {PriceId}.",
                    fallbackPlan.Id,
                    fallbackPlan.Code,
                    priceId);
                return fallbackPlan;
            }
        }

        var currentPlan = await _billingPlanRepository.GetByIdAsync(currentBillingPlanId, cancellationToken);
        if (currentPlan is not null)
        {
            _logger.LogWarning(
                "Stripe billing sync is falling back to the current workspace plan {PlanCode} ({BillingPlanId}) because price {PriceId} could not be mapped.",
                currentPlan.Code,
                currentPlan.Id,
                priceId);
            return currentPlan;
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
        var subscriptionBySubscriptionId = await _workspaceBillingRepository.GetSubscriptionByProviderSubscriptionIdAsync(
            StripeProvider,
            stripeSubscription.SubscriptionId,
            cancellationToken);
        var subscriptionByCustomerId = string.IsNullOrWhiteSpace(stripeSubscription.CustomerId)
            ? null
            : await _workspaceBillingRepository.GetLatestSubscriptionByProviderCustomerIdAsync(
                StripeProvider,
                stripeSubscription.CustomerId,
                cancellationToken);
        var pendingTransactionByCustomerId = string.IsNullOrWhiteSpace(stripeSubscription.CustomerId)
            ? null
            : await _workspaceBillingRepository.GetLatestPendingBillingTransactionByCustomerIdAsync(
                StripeProvider,
                stripeSubscription.CustomerId,
                cancellationToken);
        var relatedTransactionBySubscriptionId = await _workspaceBillingRepository.GetLatestBillingTransactionBySubscriptionIdAsync(
            StripeProvider,
            stripeSubscription.SubscriptionId,
            cancellationToken);

        var subscription = subscriptionBySubscriptionId ?? subscriptionByCustomerId;
        var resolvedWorkspaceId = subscriptionBySubscriptionId?.WorkspaceId
            ?? subscriptionByCustomerId?.WorkspaceId
            ?? pendingTransactionByCustomerId?.WorkspaceId
            ?? relatedTransactionBySubscriptionId?.WorkspaceId
            ?? stripeSubscription.WorkspaceId
            ?? fallbackWorkspaceId;

        var resolutionSource = subscriptionBySubscriptionId is not null
            ? "provider_subscription_id"
            : subscriptionByCustomerId is not null
                ? "provider_customer_id"
                : pendingTransactionByCustomerId is not null
                    ? "pending_billing_transaction"
                    : relatedTransactionBySubscriptionId is not null
                        ? "subscription_billing_transaction"
                        : stripeSubscription.WorkspaceId.HasValue
                            ? "stripe_subscription_metadata"
                            : fallbackWorkspaceId.HasValue
                                ? "fallback_workspace"
                                : "unresolved";

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

        _logger.LogInformation(
            "Resolved Stripe subscription {SubscriptionId} for event {EventType} to workspace {WorkspaceId} via {ResolutionSource}. Customer {CustomerId}, price {PriceId}, plan code {PlanCode}.",
            stripeSubscription.SubscriptionId,
            eventType,
            resolvedWorkspaceId.Value,
            resolutionSource,
            stripeSubscription.CustomerId,
            stripeSubscription.PriceId,
            stripeSubscription.PlanCode);

        await _workspaceBillingService.EnsureTrialProvisionedAsync(resolvedWorkspaceId.Value, cancellationToken);

        var latestWorkspaceSubscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            resolvedWorkspaceId.Value,
            cancellationToken);
        var currentWorkspaceSubscription = await _workspaceBillingRepository.GetCurrentSubscriptionAsync(
            resolvedWorkspaceId.Value,
            updatedAtUtc,
            cancellationToken);

        subscription ??= latestWorkspaceSubscription;

        if (subscription is not null &&
            currentWorkspaceSubscription is not null &&
            currentWorkspaceSubscription.Id != subscription.Id &&
            currentWorkspaceSubscription.Status == WorkspaceSubscriptionStatus.Trial)
        {
            _logger.LogWarning(
                "Stripe subscription {SubscriptionId} resolved to historical subscription row {ResolvedSubscriptionId} via {ResolutionSource}, but workspace {WorkspaceId} still has current trial row {CurrentTrialSubscriptionId}. Overriding the current trial row instead of updating the historical row.",
                stripeSubscription.SubscriptionId,
                subscription.Id,
                resolutionSource,
                resolvedWorkspaceId.Value,
                currentWorkspaceSubscription.Id);

            subscription = currentWorkspaceSubscription;
            resolutionSource = $"{resolutionSource}+current_trial_override";
        }

        if (subscription is null)
        {
            _logger.LogWarning(
                "Stripe subscription event {EventType} for {SubscriptionId} resolved workspace {WorkspaceId} but no subscription row was available.",
                eventType,
                stripeSubscription.SubscriptionId,
                resolvedWorkspaceId.Value);
            return null;
        }

        LogSubscriptionSnapshot(
            "Current subscription row before Stripe sync.",
            subscription);

        var plan = await ResolvePlanForStripeReferenceAsync(
            stripeSubscription.PriceId,
            stripeSubscription.PlanCode,
            subscription.BillingPlanId,
            pendingTransactionByCustomerId?.BillingPlanId
                ?? relatedTransactionBySubscriptionId?.BillingPlanId
                ?? fallbackBillingPlanId,
            cancellationToken);
        var status = MapStripeSubscriptionStatus(
            stripeSubscription.Status,
            stripeSubscription.CanceledAtUtc);

        _logger.LogInformation(
            "Stripe subscription {SubscriptionId} mapped price {PriceId} and plan code {PlanCode} to billing plan {BillingPlanId} ({ResolvedPlanCode}) with target status {TargetStatus}.",
            stripeSubscription.SubscriptionId,
            stripeSubscription.PriceId,
            stripeSubscription.PlanCode,
            plan.Id,
            plan.Code,
            status);

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

        var rowsAffected = await _workspaceBillingRepository.UpdateSubscriptionAsync(subscription, cancellationToken);
        _logger.LogInformation(
            "UpdateSubscriptionAsync affected {RowsAffected} row(s) for workspace subscription {SubscriptionId} on workspace {WorkspaceId}. Resolution source: {ResolutionSource}.",
            rowsAffected,
            subscription.Id,
            subscription.WorkspaceId,
            resolutionSource);

        if (rowsAffected == 0)
        {
            _logger.LogError(
                "Stripe subscription {SubscriptionId} did not update any workspace subscription row for workspace {WorkspaceId}.",
                stripeSubscription.SubscriptionId,
                subscription.WorkspaceId);
            throw new InvalidOperationException(
                $"Stripe subscription {stripeSubscription.SubscriptionId} did not update any workspace subscription row.");
        }

        var latestSubscriptionAfterUpdate = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            subscription.WorkspaceId,
            cancellationToken);
        var currentSubscriptionAfterUpdate = await _workspaceBillingRepository.GetCurrentSubscriptionAsync(
            subscription.WorkspaceId,
            updatedAtUtc,
            cancellationToken);

        LogSubscriptionSnapshot(
            "Latest subscription row after Stripe sync.",
            latestSubscriptionAfterUpdate);
        LogSubscriptionSnapshot(
            "Current effective subscription row after Stripe sync.",
            currentSubscriptionAfterUpdate);

        if (latestSubscriptionAfterUpdate is null)
        {
            _logger.LogError(
                "Stripe subscription {SubscriptionId} updated workspace {WorkspaceId}, but no latest subscription row could be read back afterward.",
                stripeSubscription.SubscriptionId,
                subscription.WorkspaceId);
        }
        else if (latestSubscriptionAfterUpdate.Id != subscription.Id)
        {
            _logger.LogError(
                "Stripe subscription {SubscriptionId} updated subscription row {UpdatedSubscriptionId}, but the latest subscription row for workspace {WorkspaceId} is {LatestSubscriptionId} with status {LatestStatus}.",
                stripeSubscription.SubscriptionId,
                subscription.Id,
                subscription.WorkspaceId,
                latestSubscriptionAfterUpdate.Id,
                latestSubscriptionAfterUpdate.Status);
        }

        if (currentSubscriptionAfterUpdate is not null &&
            currentSubscriptionAfterUpdate.Status == WorkspaceSubscriptionStatus.Trial)
        {
            _logger.LogError(
                "Stripe subscription {SubscriptionId} sync completed for workspace {WorkspaceId}, but the current effective subscription still resolves to trial row {CurrentSubscriptionId}.",
                stripeSubscription.SubscriptionId,
                subscription.WorkspaceId,
                currentSubscriptionAfterUpdate.Id);
        }

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

        return new StripeSubscriptionSyncContext(
            subscription,
            plan,
            pendingTransactionByCustomerId ?? relatedTransactionBySubscriptionId);
    }

    private void LogSubscriptionSnapshot(string message, WorkspaceSubscription? subscription)
    {
        if (subscription is null)
        {
            _logger.LogInformation("{Message} No subscription row was available.", message);
            return;
        }

        _logger.LogInformation(
            "{Message} Id {SubscriptionId}, workspace {WorkspaceId}, billing plan {BillingPlanId}, status {Status}, provider {Provider}, customer {ProviderCustomerId}, subscription {ProviderSubscriptionId}, price {ProviderPriceId}, trial ends {TrialEndsAtUtc}, current period {CurrentPeriodStartUtc} to {CurrentPeriodEndUtc}, canceled at {CanceledAtUtc}, updated at {UpdatedAtUtc}.",
            message,
            subscription.Id,
            subscription.WorkspaceId,
            subscription.BillingPlanId,
            subscription.Status,
            subscription.Provider,
            subscription.ProviderCustomerId,
            subscription.ProviderSubscriptionId,
            subscription.ProviderPriceId,
            subscription.TrialEndsAtUtc,
            subscription.CurrentPeriodStartUtc,
            subscription.CurrentPeriodEndUtc,
            subscription.CanceledAtUtc,
            subscription.UpdatedAtUtc);
    }

    private StripeCreditPackOptions ResolveCreditPackOrDefault(string? creditPackCode)
    {
        if (!string.IsNullOrWhiteSpace(creditPackCode))
        {
            var configuredCreditPack = _stripeOptions.CreditPacks.FirstOrDefault(option =>
                string.Equals(option.Code, creditPackCode.Trim(), StringComparison.OrdinalIgnoreCase));

            if (configuredCreditPack is not null)
            {
                return configuredCreditPack;
            }

            _logger.LogWarning(
                "Stripe credit purchase confirmation could not resolve configured credit pack {CreditPackCode}. Falling back to a generic label.",
                creditPackCode);
        }

        return new StripeCreditPackOptions
        {
            Code = creditPackCode?.Trim() ?? "credits",
            Name = "Extra credits",
            Description = "Additional workspace credits.",
            Credits = 0,
            Amount = 0m,
            CurrencyCode = "BRL"
        };
    }

    private void LogWebhookReceipt(StripeWebhookEvent webhookEvent)
    {
        switch (webhookEvent)
        {
            case StripeCheckoutCompletedEvent checkoutCompletedEvent:
                _logger.LogInformation(
                    "Received Stripe webhook event {EventType} ({EventId}) for session {CheckoutSessionId}, workspace {WorkspaceId}, customer {CustomerId}, subscription {SubscriptionId}.",
                    checkoutCompletedEvent.EventType,
                    checkoutCompletedEvent.EventId,
                    checkoutCompletedEvent.SessionId,
                    checkoutCompletedEvent.WorkspaceId,
                    checkoutCompletedEvent.CustomerId,
                    checkoutCompletedEvent.SubscriptionId);
                break;
            case StripeInvoicePaidEvent invoicePaidEvent:
                _logger.LogInformation(
                    "Received Stripe webhook event {EventType} ({EventId}) for invoice {InvoiceId}, subscription {SubscriptionId}, customer {CustomerId}.",
                    invoicePaidEvent.EventType,
                    invoicePaidEvent.EventId,
                    invoicePaidEvent.InvoiceId,
                    invoicePaidEvent.SubscriptionId,
                    invoicePaidEvent.CustomerId);
                break;
            case StripeSubscriptionUpdatedEvent subscriptionUpdatedEvent:
                _logger.LogInformation(
                    "Received Stripe webhook event {EventType} ({EventId}) for subscription {SubscriptionId}, workspace {WorkspaceId}, customer {CustomerId}.",
                    subscriptionUpdatedEvent.EventType,
                    subscriptionUpdatedEvent.EventId,
                    subscriptionUpdatedEvent.SubscriptionId,
                    subscriptionUpdatedEvent.WorkspaceId,
                    subscriptionUpdatedEvent.CustomerId);
                break;
            default:
                _logger.LogInformation(
                    "Received Stripe webhook event {EventType} ({EventId}).",
                    webhookEvent.EventType,
                    webhookEvent.EventId);
                break;
        }
    }

    private async Task TrySendPaymentConfirmedEmailAsync(
        Guid workspaceId,
        WorkspacePaymentConfirmationKind kind,
        string itemName,
        decimal amountPaid,
        string currencyCode,
        string confirmationMessage,
        long? creditsGranted,
        CancellationToken cancellationToken)
    {
        try
        {
            var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
            if (workspace is null)
            {
                _logger.LogWarning(
                    "Skipping billing confirmation email because workspace {WorkspaceId} could not be loaded.",
                    workspaceId);
                return;
            }

            var recipient = (await _workspaceMembershipRepository.ListByWorkspaceAsync(workspaceId, cancellationToken))
                .Where(member => member.IsActive)
                .OrderBy(member => member.Role switch
                {
                    Domain.Workspaces.WorkspaceRole.Owner => 0,
                    Domain.Workspaces.WorkspaceRole.Admin => 1,
                    _ => 2
                })
                .ThenBy(member => member.CreatedAtUtc)
                .FirstOrDefault();

            if (recipient is null)
            {
                _logger.LogWarning(
                    "Skipping billing confirmation email because workspace {WorkspaceId} has no active member recipient.",
                    workspaceId);
                return;
            }

            await _workspaceBillingNotifier.SendPaymentConfirmedAsync(
                new WorkspacePaymentConfirmationNotification(
                    recipient.Email,
                    recipient.FullName,
                    workspace.Name,
                    kind,
                    itemName,
                    amountPaid,
                    currencyCode,
                    confirmationMessage,
                    creditsGranted),
                cancellationToken);

            _logger.LogInformation(
                "Triggered billing confirmation email for workspace {WorkspaceId} to {Email}. Kind {PaymentKind}, item {ItemName}.",
                workspaceId,
                recipient.Email,
                kind,
                itemName);
        }
        catch (Exception exception)
        {
            _logger.LogError(
                exception,
                "Billing confirmation email failed for workspace {WorkspaceId}.",
                workspaceId);
        }
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
