namespace RootFlow.Application.Abstractions.Billing;

public interface IWorkspaceBillingNotifier
{
    Task SendPaymentConfirmedAsync(
        WorkspacePaymentConfirmationNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspacePaymentConfirmationNotification(
    string Email,
    string? FullName,
    string WorkspaceName,
    string PlanName,
    decimal AmountPaid,
    string CurrencyCode);
