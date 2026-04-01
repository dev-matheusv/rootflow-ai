using RootFlow.Domain.Workspaces;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IWorkspaceInvitationRepository
{
    Task CreateAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken = default);

    Task<WorkspaceInvitation?> GetByTokenAsync(string token, CancellationToken cancellationToken = default);

    Task<WorkspaceInvitation?> GetPendingForWorkspaceEmailAsync(
        Guid workspaceId,
        string normalizedEmail,
        CancellationToken cancellationToken = default);

    Task UpdateAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken = default);
}
