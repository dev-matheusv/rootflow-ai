using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IWorkspaceRepository
{
    Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default);

    Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken = default);

    Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default);
}
