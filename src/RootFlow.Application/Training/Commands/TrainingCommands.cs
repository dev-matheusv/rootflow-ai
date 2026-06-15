using RootFlow.Domain.Training;

namespace RootFlow.Application.Training.Commands;

public sealed record CreateTrainingProgramCommand(
    Guid WorkspaceId,
    Guid CreatedByUserId,
    string Name,
    string? Slug,
    string? Description);

public sealed record UpdateTrainingProgramCommand(
    Guid ProgramId,
    Guid WorkspaceId,
    string Name,
    string? Description,
    int PassingScore);

public sealed record PublishTrainingProgramCommand(
    Guid ProgramId,
    Guid WorkspaceId);

public sealed record AddTrainingModuleCommand(
    Guid ProgramId,
    Guid WorkspaceId,
    string Title,
    string? Description,
    int OrderIndex,
    IReadOnlyList<Guid> SourceDocumentIds);

public sealed record UpdateTrainingModuleCommand(
    Guid ModuleId,
    Guid WorkspaceId,
    string Title,
    string? Description,
    int OrderIndex,
    IReadOnlyList<Guid> SourceDocumentIds);

public sealed record DeleteTrainingModuleCommand(
    Guid ModuleId,
    Guid WorkspaceId);

public sealed record GenerateTrainingQuizCommand(
    Guid ModuleId,
    Guid WorkspaceId,
    int QuestionCount);

public sealed record UpdateTrainingQuestionCommand(
    Guid QuestionId,
    Guid WorkspaceId,
    string Prompt,
    TrainingQuestionType Type,
    IReadOnlyList<string> Options,
    IReadOnlyList<int> CorrectAnswerIndices,
    string? Explanation);

public sealed record PublishTrainingQuestionCommand(
    Guid QuestionId,
    Guid WorkspaceId);

public sealed record DeleteTrainingQuestionCommand(
    Guid QuestionId,
    Guid WorkspaceId);
