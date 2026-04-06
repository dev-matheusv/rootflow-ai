namespace RootFlow.Application.Abstractions.Billing;

public interface IWorkspaceBillingNotifier
{
    Task SendPaymentConfirmedAsync(
        WorkspacePaymentConfirmationNotification notification,
        CancellationToken cancellationToken = default);

    Task SendLifecycleNotificationAsync(
        WorkspaceBillingLifecycleNotification notification,
        CancellationToken cancellationToken = default);

    Task SendPlatformAlertAsync(
        PlatformBillingAlertNotification notification,
        CancellationToken cancellationToken = default);
}

public enum WorkspacePaymentConfirmationKind
{
    Subscription = 1,
    CreditPurchase = 2
}

public sealed record WorkspacePaymentConfirmationNotification(
    string Email,
    string? FullName,
    string WorkspaceName,
    WorkspacePaymentConfirmationKind Kind,
    string ItemName,
    decimal AmountPaid,
    string CurrencyCode,
    string ConfirmationMessage,
    long? CreditsGranted = null);

public enum WorkspaceBillingLifecycleNotificationKind
{
    TrialExpiring = 1,
    LowCredits = 2,
    CriticalCredits = 3,
    NoCredits = 4
}

public sealed record WorkspaceBillingLifecycleNotification(
    string Email,
    string? FullName,
    string WorkspaceName,
    WorkspaceBillingLifecycleNotificationKind Kind,
    string? PlanName,
    long AvailableCredits,
    int? RemainingPercent = null,
    DateTime? TrialEndsAtUtc = null,
    int? TrialDaysRemaining = null);

public sealed record PlatformBillingAlertNotification(
    string Email,
    string? FullName,
    int PaymentIssueCount,
    int ReplayableWebhookCount,
    IReadOnlyList<string> DetailLines);
