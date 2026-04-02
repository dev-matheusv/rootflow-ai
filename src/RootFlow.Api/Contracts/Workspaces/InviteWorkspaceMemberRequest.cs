namespace RootFlow.Api.Contracts.Workspaces;

public sealed record InviteWorkspaceMemberRequest(
    string Email,
    string? Role);
