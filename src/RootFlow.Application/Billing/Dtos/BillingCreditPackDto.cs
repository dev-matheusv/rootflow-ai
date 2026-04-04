namespace RootFlow.Application.Billing.Dtos;

public sealed record BillingCreditPackDto(
    string Code,
    string Name,
    string Description,
    long Credits,
    decimal Amount,
    string CurrencyCode,
    bool IsConfigured);
