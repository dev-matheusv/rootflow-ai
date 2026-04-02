using RootFlow.Application.Abstractions.Workspaces;

namespace RootFlow.Api.IntegrationTests.Infrastructure;

public sealed class TestWorkspaceInvitationNotifier : IWorkspaceInvitationNotifier
{
    private readonly object _sync = new();
    private readonly List<WorkspaceInvitationNotification> _notifications = [];

    public Task SendInviteLinkAsync(
        WorkspaceInvitationNotification notification,
        CancellationToken cancellationToken = default)
    {
        lock (_sync)
        {
            _notifications.Add(notification);
        }

        return Task.CompletedTask;
    }

    public WorkspaceInvitationNotification? GetLatestForEmail(string email)
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
