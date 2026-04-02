using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Workspaces.Dtos;

public sealed record WorkspaceMemberDto(
    Guid UserId,
    string FullName,
    string Email,
    WorkspaceRole Role,
    DateTime CreatedAtUtc,
    bool IsCurrentUser);
