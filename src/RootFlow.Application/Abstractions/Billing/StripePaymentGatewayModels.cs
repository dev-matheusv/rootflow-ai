namespace RootFlow.Application.Abstractions.Billing;

public sealed record StripeSubscriptionCheckoutRequest(
    Guid WorkspaceId,
    string PlanCode,
    string PriceId,
    string SuccessUrl,
    string CancelUrl,
    string? ExistingCustomerId = null);

public sealed record StripeCreditPurchaseCheckoutRequest(
    Guid WorkspaceId,
    string CreditPackCode,
    long Credits,
    string PriceId,
    string SuccessUrl,
    string CancelUrl,
    string? ExistingCustomerId = null);

public sealed record StripeCheckoutSessionResult(
    string SessionId,
    string Url,
    string? CustomerId,
    string? SubscriptionId,
    string? PaymentIntentId);

public abstract record StripeWebhookEvent(
    string EventId,
    string EventType,
    DateTime OccurredAtUtc);

public sealed record StripeCheckoutCompletedEvent(
    string EventId,
    DateTime OccurredAtUtc,
    string SessionId,
    string Mode,
    string PaymentStatus,
    Guid? WorkspaceId,
    string? PlanCode,
    string? CreditPackCode,
    long? CreditAmount,
    string? CustomerId,
    string? SubscriptionId,
    string? PaymentIntentId)
    : StripeWebhookEvent(EventId, "checkout.session.completed", OccurredAtUtc);

public sealed record StripeInvoicePaidEvent(
    string EventId,
    DateTime OccurredAtUtc,
    string InvoiceId,
    string SubscriptionId,
    string? CustomerId,
    string? PriceId,
    decimal AmountPaid,
    string CurrencyCode,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc)
    : StripeWebhookEvent(EventId, "invoice.paid", OccurredAtUtc);

public sealed record StripeSubscriptionUpdatedEvent(
    string EventId,
    string EventType,
    DateTime OccurredAtUtc,
    string SubscriptionId,
    string? CustomerId,
    string? PriceId,
    string Status,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    DateTime? CanceledAtUtc)
    : StripeWebhookEvent(EventId, EventType, OccurredAtUtc);

public sealed record StripeUnhandledWebhookEvent(
    string EventId,
    string EventType,
    DateTime OccurredAtUtc)
    : StripeWebhookEvent(EventId, EventType, OccurredAtUtc);
