using RootFlow.Application.Abstractions.Auth;

namespace RootFlow.Api.IntegrationTests.Infrastructure;

public sealed class TestPasswordResetNotifier : IPasswordResetNotifier
{
    private readonly object _sync = new();
    private readonly List<PasswordResetNotification> _notifications = [];

    public Task SendResetLinkAsync(
        PasswordResetNotification notification,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _notifications.Add(notification);
        }

        return Task.CompletedTask;
    }

    public PasswordResetNotification? GetLatestForEmail(string email)
    {
        lock (_sync)
        {
            return _notifications.LastOrDefault(notification =>
                string.Equals(notification.Email, email, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void Clear()
    {
        lock (_sync)
        {
            _notifications.Clear();
        }
    }
}
