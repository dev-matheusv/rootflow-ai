using RootFlow.Application.Abstractions.Billing;

namespace RootFlow.Api.IntegrationTests.Infrastructure;

public sealed class TestWorkspaceBillingNotifier : IWorkspaceBillingNotifier
{
    private readonly object _sync = new();
    private readonly List<WorkspacePaymentConfirmationNotification> _paymentConfirmations = [];
    private readonly List<WorkspaceBillingLifecycleNotification> _lifecycleNotifications = [];
    private readonly List<PlatformBillingAlertNotification> _platformAlerts = [];

    public Task SendPaymentConfirmedAsync(
        WorkspacePaymentConfirmationNotification notification,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _paymentConfirmations.Add(notification);
        }

        return Task.CompletedTask;
    }

    public Task SendLifecycleNotificationAsync(
        WorkspaceBillingLifecycleNotification notification,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _lifecycleNotifications.Add(notification);
        }

        return Task.CompletedTask;
    }

    public Task SendPlatformAlertAsync(
        PlatformBillingAlertNotification notification,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _platformAlerts.Add(notification);
        }

        return Task.CompletedTask;
    }

    public IReadOnlyList<WorkspacePaymentConfirmationNotification> PaymentConfirmations
    {
        get
        {
            lock (_sync)
            {
                return _paymentConfirmations.ToArray();
            }
        }
    }

    public IReadOnlyList<WorkspaceBillingLifecycleNotification> LifecycleNotifications
    {
        get
        {
            lock (_sync)
            {
                return _lifecycleNotifications.ToArray();
            }
        }
    }

    public IReadOnlyList<PlatformBillingAlertNotification> PlatformAlerts
    {
        get
        {
            lock (_sync)
            {
                return _platformAlerts.ToArray();
            }
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _paymentConfirmations.Clear();
            _lifecycleNotifications.Clear();
            _platformAlerts.Clear();
        }
    }
}
