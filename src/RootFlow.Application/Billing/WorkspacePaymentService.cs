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
    private readonly IAppLinkBuilder _appLinkBuilder;
    private readonly IClock _clock;
    private readonly StripeBillingOptions _stripeOptions;
    private readonly ILogger<WorkspacePaymentService> _logger;

    public WorkspacePaymentService(
        IWorkspaceRepository workspaceRepository,
        IBillingPlanRepository billingPlanRepository,
        IWorkspaceBillingRepository workspaceBillingRepository,
        WorkspaceBillingService workspaceBillingService,
        IStripePaymentGateway stripePaymentGateway,
        IAppLinkBuilder appLinkBuilder,
        IClock clock,
        StripeBillingOptions stripeOptions,
        ILogger<WorkspacePaymentService> logger)
    {
        _workspaceRepository = workspaceRepository;
        _billingPlanRepository = billingPlanRepository;
        _workspaceBillingRepository = workspaceBillingRepository;
        _workspaceBillingService = workspaceBillingService;
        _stripePaymentGateway = stripePaymentGateway;
        _appLinkBuilder = appLinkBuilder;
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
                !string.IsNullOrWhiteSpace(pack.PriceId)))
            .ToArray();

        return Task.FromResult(creditPacks);
    }

    public async Task<BillingCheckoutSessionDto> CreateSubscriptionCheckoutAsync(
        CreateWorkspaceSubscriptionCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        await EnsureWorkspaceExistsAsync(command.WorkspaceId, cancellationToken);
        await _workspaceBillingService.EnsureTrialProvisionedAsync(command.WorkspaceId, cancellationToken);

        var plan = await _billingPlanRepository.GetByCodeAsync(command.PlanCode, cancellationToken);
        if (plan is null || !plan.IsActive)
        {
            throw new ArgumentException("The selected billing plan is not available.", nameof(command.PlanCode));
        }

        var priceId = ResolvePlanPriceId(plan.Code);
        var existingSubscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            command.WorkspaceId,
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
                command.WorkspaceId,
                plan.Code,
                priceId,
                BuildCheckoutReturnUrl(_stripeOptions.CheckoutSuccessPath, "subscription"),
                BuildCheckoutReturnUrl(_stripeOptions.CheckoutCancelPath, "subscription"),
                existingSubscription?.Provider == StripeProvider
                    ? existingSubscription.ProviderCustomerId
                    : null),
            cancellationToken);

        var transaction = new WorkspaceBillingTransaction(
            Guid.NewGuid(),
            command.WorkspaceId,
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
            command.WorkspaceId,
            plan.Code);

        return new BillingCheckoutSessionDto(checkoutSession.SessionId, checkoutSession.Url);
    }

    public async Task<BillingCheckoutSessionDto> CreateCreditPurchaseCheckoutAsync(
        CreateWorkspaceCreditPurchaseCheckoutCommand command,
        CancellationToken cancellationToken = default)
    {
        await EnsureWorkspaceExistsAsync(command.WorkspaceId, cancellationToken);
        await _workspaceBillingService.EnsureTrialProvisionedAsync(command.WorkspaceId, cancellationToken);

        var creditPack = ResolveCreditPack(command.CreditPackCode);
        var existingSubscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
            command.WorkspaceId,
            cancellationToken);
        var now = _clock.UtcNow;
        var checkoutSession = await _stripePaymentGateway.CreateCreditPurchaseCheckoutSessionAsync(
            new StripeCreditPurchaseCheckoutRequest(
                command.WorkspaceId,
                creditPack.Code,
                creditPack.Credits,
                creditPack.PriceId,
                BuildCheckoutReturnUrl(_stripeOptions.CheckoutSuccessPath, "credits"),
                BuildCheckoutReturnUrl(_stripeOptions.CheckoutCancelPath, "credits"),
                existingSubscription?.Provider == StripeProvider
                    ? existingSubscription.ProviderCustomerId
                    : null),
            cancellationToken);

        var transaction = new WorkspaceBillingTransaction(
            Guid.NewGuid(),
            command.WorkspaceId,
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
            command.WorkspaceId,
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
        if (subscription is null)
        {
            relatedCheckoutTransaction = await _workspaceBillingRepository.GetLatestBillingTransactionBySubscriptionIdAsync(
                StripeProvider,
                invoicePaidEvent.SubscriptionId,
                cancellationToken);

            if (relatedCheckoutTransaction is null)
            {
                _logger.LogWarning(
                    "Stripe invoice {InvoiceId} could not be matched to a workspace subscription or checkout transaction.",
                    invoicePaidEvent.InvoiceId);
                return;
            }

            await _workspaceBillingService.EnsureTrialProvisionedAsync(
                relatedCheckoutTransaction.WorkspaceId,
                cancellationToken);

            subscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
                relatedCheckoutTransaction.WorkspaceId,
                cancellationToken);
        }

        if (subscription is null)
        {
            _logger.LogWarning(
                "Stripe invoice {InvoiceId} could not resolve a billable workspace subscription after provisioning.",
                invoicePaidEvent.InvoiceId);
            return;
        }

        var plan = await ResolvePlanForInvoiceAsync(
            invoicePaidEvent.PriceId,
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
            "Applied Stripe invoice {InvoiceId} for workspace {WorkspaceId}, subscription {SubscriptionId}, plan {PlanCode}.",
            invoicePaidEvent.InvoiceId,
            subscription.WorkspaceId,
            invoicePaidEvent.SubscriptionId,
            plan.Code);
    }

    private async Task HandleSubscriptionUpdatedAsync(
        StripeSubscriptionUpdatedEvent subscriptionUpdatedEvent,
        CancellationToken cancellationToken)
    {
        var subscription = await _workspaceBillingRepository.GetSubscriptionByProviderSubscriptionIdAsync(
            StripeProvider,
            subscriptionUpdatedEvent.SubscriptionId,
            cancellationToken);

        WorkspaceBillingTransaction? relatedTransaction = null;
        if (subscription is null)
        {
            relatedTransaction = await _workspaceBillingRepository.GetLatestBillingTransactionBySubscriptionIdAsync(
                StripeProvider,
                subscriptionUpdatedEvent.SubscriptionId,
                cancellationToken);

            if (relatedTransaction is null)
            {
                _logger.LogWarning(
                    "Stripe subscription event {EventType} for {SubscriptionId} could not be matched to a workspace.",
                    subscriptionUpdatedEvent.EventType,
                    subscriptionUpdatedEvent.SubscriptionId);
                return;
            }

            await _workspaceBillingService.EnsureTrialProvisionedAsync(relatedTransaction.WorkspaceId, cancellationToken);
            subscription = await _workspaceBillingRepository.GetLatestSubscriptionAsync(
                relatedTransaction.WorkspaceId,
                cancellationToken);
        }

        if (subscription is null)
        {
            _logger.LogWarning(
                "Stripe subscription event {EventType} for {SubscriptionId} could not resolve a subscription row.",
                subscriptionUpdatedEvent.EventType,
                subscriptionUpdatedEvent.SubscriptionId);
            return;
        }

        var plan = await ResolvePlanForInvoiceAsync(
            subscriptionUpdatedEvent.PriceId,
            subscription.BillingPlanId,
            relatedTransaction?.BillingPlanId,
            cancellationToken);

        var status = MapStripeSubscriptionStatus(
            subscriptionUpdatedEvent.Status,
            subscriptionUpdatedEvent.CanceledAtUtc);

        subscription.SyncProviderSubscription(
            plan.Id,
            status,
            subscriptionUpdatedEvent.CurrentPeriodStartUtc,
            subscriptionUpdatedEvent.CurrentPeriodEndUtc,
            subscriptionUpdatedEvent.OccurredAtUtc,
            StripeProvider,
            subscriptionUpdatedEvent.CustomerId,
            subscriptionUpdatedEvent.SubscriptionId,
            subscriptionUpdatedEvent.PriceId,
            subscriptionUpdatedEvent.CanceledAtUtc);

        await _workspaceBillingRepository.UpdateSubscriptionAsync(subscription, cancellationToken);

        _logger.LogInformation(
            "Synced Stripe subscription {SubscriptionId} for workspace {WorkspaceId} to status {SubscriptionStatus}.",
            subscriptionUpdatedEvent.SubscriptionId,
            subscription.WorkspaceId,
            status);
    }

    private async Task EnsureWorkspaceExistsAsync(Guid workspaceId, CancellationToken cancellationToken)
    {
        var workspaceExists = await _workspaceRepository.ExistsAsync(workspaceId, cancellationToken);
        if (!workspaceExists)
        {
            throw new InvalidOperationException("Workspace was not found.");
        }
    }

    private async Task<BillingPlan> ResolvePlanForInvoiceAsync(
        string? priceId,
        Guid currentBillingPlanId,
        Guid? fallbackBillingPlanId,
        CancellationToken cancellationToken)
    {
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

    private string BuildCheckoutReturnUrl(string routePathWithQuery, string flow)
    {
        var separator = routePathWithQuery.Contains('?') ? "&" : "?";
        var route = $"{routePathWithQuery}{separator}flow={Uri.EscapeDataString(flow)}&session_id={{CHECKOUT_SESSION_ID}}";
        return _appLinkBuilder.BuildAppRouteLink(route, requireAbsoluteUrl: true);
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
}
