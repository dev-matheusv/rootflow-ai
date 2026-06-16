using RootFlow.Application.Abstractions.Persistence;

namespace RootFlow.Application.Training;

// Centralizes the "is the T&D add-on enabled for this workspace" check so
// the three training services (authoring, consumer, certificates) all
// share the same guard. Throws WorkspaceTrainingDisabledException when
// the feature flag is off — caught at the API boundary and mapped to 403.
public sealed class TrainingFeatureGate
{
    private readonly IWorkspaceRepository _workspaceRepository;

    public TrainingFeatureGate(IWorkspaceRepository workspaceRepository)
    {
        _workspaceRepository = workspaceRepository;
    }

    public async Task EnsureEnabledAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceRepository.GetByIdAsync(workspaceId, cancellationToken);
        if (workspace is null || !workspace.TrainingEnabled)
        {
            throw new WorkspaceTrainingDisabledException();
        }
    }
}
