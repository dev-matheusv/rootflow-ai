using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Workspaces.Commands;

public sealed record InviteWorkspaceMembersBulkCommand(
    Guid WorkspaceId,
    Guid InvitedByUserId,
    IReadOnlyList<string> Emails,
    WorkspaceRole Role);
