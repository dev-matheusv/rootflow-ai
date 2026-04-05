using RootFlow.Domain.Billing;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IWorkspaceBillingRepository
{
    Task EnsureProvisionedAsync(
        WorkspaceSubscription subscription,
        WorkspaceCreditBalance balance,
        WorkspaceCreditLedgerEntry? initialGrantEntry,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSubscription?> GetCurrentSubscriptionAsync(
        Guid workspaceId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSubscription?> GetLatestSubscriptionAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSubscription?> GetSubscriptionByProviderSubscriptionIdAsync(
        string provider,
        string providerSubscriptionId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceSubscription?> GetLatestSubscriptionByProviderCustomerIdAsync(
        string provider,
        string providerCustomerId,
        CancellationToken cancellationToken = default);

    Task<int> UpdateSubscriptionAsync(
        WorkspaceSubscription subscription,
        CancellationToken cancellationToken = default);

    Task<WorkspaceCreditBalance?> GetCreditBalanceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceCreditBalance> AppendLedgerEntryAsync(
        WorkspaceCreditLedgerEntry entry,
        CancellationToken cancellationToken = default);

    Task<bool> LedgerReferenceExistsAsync(
        string referenceType,
        string referenceId,
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

    Task AddBillingTransactionAsync(
        WorkspaceBillingTransaction transaction,
        CancellationToken cancellationToken = default);

    Task<WorkspaceBillingTransaction?> GetBillingTransactionByCheckoutSessionIdAsync(
        string provider,
        string externalCheckoutSessionId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceBillingTransaction?> GetBillingTransactionByInvoiceIdAsync(
        string provider,
        string externalInvoiceId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceBillingTransaction?> GetLatestBillingTransactionBySubscriptionIdAsync(
        string provider,
        string externalSubscriptionId,
        CancellationToken cancellationToken = default);

    Task<WorkspaceBillingTransaction?> GetLatestPendingBillingTransactionByCustomerIdAsync(
        string provider,
        string externalCustomerId,
        CancellationToken cancellationToken = default);

    Task UpdateBillingTransactionAsync(
        WorkspaceBillingTransaction transaction,
        CancellationToken cancellationToken = default);
}
