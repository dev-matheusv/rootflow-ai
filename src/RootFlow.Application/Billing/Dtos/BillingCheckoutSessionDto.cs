namespace RootFlow.Application.Billing.Dtos;

public sealed record BillingCheckoutSessionDto(
    string SessionId,
    string CheckoutUrl);
