namespace RootFlow.Application.Abstractions.Billing;

public interface IWorkspaceBillingNotifier
{
    Task SendPaymentConfirmedAsync(
        WorkspacePaymentConfirmationNotification notification,
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
