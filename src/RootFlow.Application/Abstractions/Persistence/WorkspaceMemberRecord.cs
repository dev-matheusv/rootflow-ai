using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Persistence;

public sealed record WorkspaceMemberRecord(
    Guid UserId,
    string FullName,
    string Email,
    WorkspaceRole Role,
    DateTime CreatedAtUtc,
    bool IsActive);
