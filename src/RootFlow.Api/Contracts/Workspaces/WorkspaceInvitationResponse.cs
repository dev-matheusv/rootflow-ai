namespace RootFlow.Api.Contracts.Workspaces;

public sealed record WorkspaceInvitationResponse(
    string Message,
    string Email,
    string Role,
    DateTime ExpiresAtUtc);
