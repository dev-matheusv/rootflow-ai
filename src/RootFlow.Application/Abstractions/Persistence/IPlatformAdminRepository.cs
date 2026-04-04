using RootFlow.Application.PlatformAdmin.Dtos;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IPlatformAdminRepository
{
    Task<PlatformAdminOverviewDto> GetOverviewAsync(
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformAdminUsageWindowDto>> ListUsageWindowsAsync(
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformAdminWorkspaceSummaryDto>> ListWorkspaceSummariesAsync(
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformAdminModelUsageDto>> ListModelBreakdownAsync(
        int take,
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformAdminBillingTransactionDto>> ListRecentCreditPurchasesAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformAdminSubscriptionActivityDto>> ListRecentSubscriptionChangesAsync(
        int take,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PlatformAdminPaymentIssueDto>> ListPaymentIssuesAsync(
        int take,
        int pendingThresholdMinutes,
        CancellationToken cancellationToken = default);
}
