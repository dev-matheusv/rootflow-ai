using RootFlow.Domain.Billing;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IWorkspaceBillingRepository
{
    Task EnsureProvisionedAsync(
        WorkspaceSubscription subscription,
        WorkspaceCreditBalance balance,
        WorkspaceCreditLedgerEntry initialGrantEntry,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSubscription?> GetCurrentSubscriptionAsync(
        Guid workspaceId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSubscription?> GetLatestSubscriptionAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceCreditBalance?> GetCreditBalanceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceCreditBalance> AppendLedgerEntryAsync(
        WorkspaceCreditLedgerEntry entry,
        CancellationToken cancellationToken = default);

    Task AddUsageEventAsync(
        WorkspaceUsageEvent usageEvent,
        CancellationToken cancellationToken = default);

    Task<WorkspaceCreditBalance> RecordUsageAsync(
        WorkspaceUsageEvent usageEvent,
        WorkspaceCreditLedgerEntry debitEntry,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceCreditLedgerEntry>> ListLedgerEntriesAsync(
        Guid workspaceId,
        int take = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceUsageEvent>> ListUsageEventsAsync(
        Guid workspaceId,
        int take = 100,
        CancellationToken cancellationToken = default);
}
