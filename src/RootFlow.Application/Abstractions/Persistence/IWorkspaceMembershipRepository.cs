using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IWorkspaceMembershipRepository
{
    Task<WorkspaceMembership?> GetAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default);

    Task AddAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<WorkspaceMemberRecord>> ListByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
