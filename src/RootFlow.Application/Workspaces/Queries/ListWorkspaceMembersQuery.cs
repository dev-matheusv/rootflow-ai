namespace RootFlow.Application.Workspaces.Queries;

public sealed record ListWorkspaceMembersQuery(
    Guid WorkspaceId,
    Guid CurrentUserId);
