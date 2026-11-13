using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Workspaces.Commands;

public sealed record ChangeWorkspaceMemberRoleCommand(
    Guid WorkspaceId,
    Guid ActorUserId,
    Guid TargetUserId,
    WorkspaceRole Role);
