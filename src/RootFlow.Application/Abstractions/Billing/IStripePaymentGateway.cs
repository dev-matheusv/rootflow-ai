namespace RootFlow.Application.Abstractions.Billing;

public interface IStripePaymentGateway
{
    Task<StripeCheckoutSessionResult> CreateSubscriptionCheckoutSessionAsync(
        StripeSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken = default);

    Task<StripeCheckoutSessionResult> CreateCreditPurchaseCheckoutSessionAsync(
        StripeCreditPurchaseCheckoutRequest request,
        CancellationToken cancellationToken = default);

    StripeWebhookEvent ParseWebhook(string payload, string signatureHeader);
}
