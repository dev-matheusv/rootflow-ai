namespace RootFlow.Application.Training.Commands;

public sealed record StartTrainingAttemptCommand(
    Guid ModuleId,
    Guid WorkspaceId,
    Guid UserId);

public sealed record SubmitTrainingAnswerCommand(
    Guid AttemptId,
    Guid QuestionId,
    Guid UserId,
    IReadOnlyList<int> SelectedIndices);

public sealed record SubmitTrainingAttemptCommand(
    Guid AttemptId,
    Guid UserId);
