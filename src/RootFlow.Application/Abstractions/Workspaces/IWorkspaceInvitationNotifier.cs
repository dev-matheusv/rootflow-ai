using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Workspaces;

public interface IWorkspaceInvitationNotifier
{
    /// <summary>
    /// RF-18: false when invites cannot actually reach the person (no outbound e-mail configured in
    /// production). The UI must not claim the invite was sent in that case.
    /// </summary>
    bool CanDeliver => true;

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
