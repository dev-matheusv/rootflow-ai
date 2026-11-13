using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Workspaces;

/// <summary>
/// Checks, on every authenticated request, that the user still belongs to the workspace in the token.
/// The JWT lives for hours; without this check a removed member or a demoted admin would keep the
/// old access until the token expires.
/// </summary>
public interface IWorkspaceAccessValidator
{
    /// <returns>The current role, or null when the user, the membership or the workspace is inactive or missing.</returns>
    Task<WorkspaceRole?> GetActiveRoleAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);
}
