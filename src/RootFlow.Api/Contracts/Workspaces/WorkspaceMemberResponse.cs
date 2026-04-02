namespace RootFlow.Api.Contracts.Workspaces;

public sealed record WorkspaceMemberResponse(
    Guid UserId,
    string FullName,
    string Email,
    string Role,
    DateTime CreatedAtUtc,
    bool IsCurrentUser);
