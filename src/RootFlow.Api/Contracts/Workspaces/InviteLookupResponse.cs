namespace RootFlow.Api.Contracts.Workspaces;

public sealed record InviteLookupResponse(
    string Email,
    string WorkspaceName,
    string InviterName,
    bool IsExistingUser,
    bool IsValid,
    string? ErrorMessage = null);
