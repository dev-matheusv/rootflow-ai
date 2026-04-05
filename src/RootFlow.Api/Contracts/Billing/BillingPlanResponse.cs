namespace RootFlow.Api.Contracts.Billing;

public sealed record BillingPlanResponse(
    Guid Id,
    string Code,
    string Name,
    decimal MonthlyPrice,
    string CurrencyCode,
    long IncludedCredits,
    int MaxUsers,
    bool IsActive,
    string? PriceId);
