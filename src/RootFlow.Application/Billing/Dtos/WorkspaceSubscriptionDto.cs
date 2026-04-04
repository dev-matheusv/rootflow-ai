using RootFlow.Domain.Billing;

namespace RootFlow.Application.Billing.Dtos;

public sealed record WorkspaceSubscriptionDto(
    Guid Id,
    Guid WorkspaceId,
    Guid BillingPlanId,
    WorkspaceSubscriptionStatus Status,
    DateTime CurrentPeriodStartUtc,
    DateTime CurrentPeriodEndUtc,
    DateTime? CanceledAtUtc,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
