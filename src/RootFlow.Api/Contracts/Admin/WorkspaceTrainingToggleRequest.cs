namespace RootFlow.Api.Contracts.Admin;

public sealed record WorkspaceTrainingToggleRequest(bool Enabled);

public sealed record WorkspaceTrainingToggleResponse(Guid WorkspaceId, bool TrainingEnabled);
