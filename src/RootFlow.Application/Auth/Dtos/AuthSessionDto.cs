using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Auth.Dtos;

public sealed record AuthUserDto(
    Guid Id,
    string FullName,
    string Email);

public sealed record AuthWorkspaceDto(
    Guid Id,
    string Name,
    string Slug);

public sealed record AuthSessionDto(
    AuthUserDto User,
    AuthWorkspaceDto Workspace,
    WorkspaceRole Role,
    bool IsPlatformAdmin = false);
