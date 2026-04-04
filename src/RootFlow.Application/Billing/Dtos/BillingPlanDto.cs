namespace RootFlow.Application.Billing.Dtos;

public sealed record BillingPlanDto(
    Guid Id,
    string Code,
    string Name,
    decimal MonthlyPrice,
    string CurrencyCode,
    long IncludedCredits,
    int MaxUsers,
    bool IsActive);
