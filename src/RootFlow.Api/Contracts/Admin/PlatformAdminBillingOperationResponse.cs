namespace RootFlow.Api.Contracts.Admin;

public sealed record PlatformAdminReplayStripeWebhooksResponse(
    int ReplayedCount,
    string Message);

public sealed record PlatformAdminBillingMonitoringRunResponse(
    int AdminAlertsSent,
    int WorkspaceNotificationsSent,
    int PaymentIssueCount,
    int ReplayableWebhookCount,
    string Message);
