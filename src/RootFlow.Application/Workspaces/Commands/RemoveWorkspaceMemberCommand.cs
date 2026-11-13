namespace RootFlow.Application.Workspaces.Commands;

public sealed record RemoveWorkspaceMemberCommand(
    Guid WorkspaceId,
    Guid ActorUserId,
    Guid TargetUserId);
