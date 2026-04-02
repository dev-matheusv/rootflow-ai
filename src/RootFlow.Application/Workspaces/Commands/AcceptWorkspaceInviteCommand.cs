namespace RootFlow.Application.Workspaces.Commands;

public sealed record AcceptWorkspaceInviteCommand(
    Guid CurrentUserId,
    string Token);
