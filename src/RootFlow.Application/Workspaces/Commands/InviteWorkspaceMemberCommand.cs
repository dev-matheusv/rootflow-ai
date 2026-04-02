using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Workspaces.Commands;

public sealed record InviteWorkspaceMemberCommand(
    Guid WorkspaceId,
    Guid InvitedByUserId,
    string Email,
    WorkspaceRole Role);
