using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Workspaces;

public interface IWorkspaceInvitationNotifier
{
    Task SendInviteLinkAsync(
        WorkspaceInvitationNotification notification,
        CancellationToken cancellationToken = default);
}

public sealed record WorkspaceInvitationNotification(
    string Email,
    string WorkspaceName,
    string InvitedByFullName,
    WorkspaceRole Role,
    string Token,
    DateTime ExpiresAtUtc);
