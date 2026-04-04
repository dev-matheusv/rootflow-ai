namespace RootFlow.Api.Contracts.Billing;

public sealed record BillingCreditPackResponse(
    string Code,
    string Name,
    string Description,
    long Credits,
    decimal Amount,
    string CurrencyCode,
    bool IsConfigured);
