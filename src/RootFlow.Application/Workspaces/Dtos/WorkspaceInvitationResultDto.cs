using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Workspaces.Dtos;

public sealed record WorkspaceInvitationResultDto(
    string Message,
    string Email,
    WorkspaceRole Role,
    DateTime ExpiresAtUtc);
