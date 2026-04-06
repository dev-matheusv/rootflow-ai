namespace RootFlow.Api.Contracts.Billing;

public sealed record BillingCheckoutRedirectResponse(
    string SessionId,
    string Url);
