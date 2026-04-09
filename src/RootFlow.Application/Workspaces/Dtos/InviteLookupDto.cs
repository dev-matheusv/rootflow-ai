namespace RootFlow.Application.Workspaces.Dtos;

public sealed record InviteLookupDto(
    string Email,
    string WorkspaceName,
    string InviterName,
    bool IsExistingUser,
    bool IsValid,
    string? ErrorMessage = null);
