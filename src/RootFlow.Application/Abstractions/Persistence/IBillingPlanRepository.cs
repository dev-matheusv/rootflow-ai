using RootFlow.Domain.Billing;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IBillingPlanRepository
{
    Task<BillingPlan?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default);

    Task<BillingPlan?> GetByIdAsync(
        Guid billingPlanId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingPlan>> ListAllAsync(
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<BillingPlan>> ListActiveAsync(
        CancellationToken cancellationToken = default);
}
