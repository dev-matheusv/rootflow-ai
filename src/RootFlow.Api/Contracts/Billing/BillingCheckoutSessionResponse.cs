namespace RootFlow.Api.Contracts.Billing;

public sealed record BillingCheckoutSessionResponse(
    string SessionId,
    string CheckoutUrl);
