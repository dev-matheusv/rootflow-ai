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

    Task<long> EnsureCreditGrantTargetAsync(
        Guid workspaceId,
        WorkspaceCreditLedgerType type,
        long targetAmount,
        string description,
        DateTime createdAtUtc,
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

    Task<WorkspaceBillingTransaction?> GetBillingTransactionByIdAsync(
        Guid transactionId,
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

    Task<WorkspaceBillingWebhookEvent> UpsertBillingWebhookEventAsync(
        WorkspaceBillingWebhookEvent webhookEvent,
        CancellationToken cancellationToken = default);

    Task<WorkspaceBillingWebhookEvent?> GetBillingWebhookEventByProviderEventIdAsync(
        string provider,
        string providerEventId,
        CancellationToken cancellationToken = default);

    Task<bool> TryStartBillingWebhookEventProcessingAsync(
        Guid webhookEventId,
        DateTime startedAtUtc,
        DateTime? staleProcessingBeforeUtc = null,
        CancellationToken cancellationToken = default);

    Task MarkBillingWebhookEventProcessedAsync(
        Guid webhookEventId,
        DateTime processedAtUtc,
        CancellationToken cancellationToken = default);

    Task MarkBillingWebhookEventFailedAsync(
        Guid webhookEventId,
        string error,
        DateTime failedAtUtc,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceBillingWebhookEvent>> ListReplayableBillingWebhookEventsAsync(
        string provider,
        int take,
        DateTime failedBeforeUtc,
        DateTime staleProcessingBeforeUtc,
        CancellationToken cancellationToken = default);

    Task<bool> BillingNotificationDeliveryExistsAsync(
        string notificationKind,
        string dedupeKey,
        string recipientEmail,
        CancellationToken cancellationToken = default);

    Task RecordBillingNotificationDeliveryAsync(
        WorkspaceBillingNotificationDelivery delivery,
        CancellationToken cancellationToken = default);
}
