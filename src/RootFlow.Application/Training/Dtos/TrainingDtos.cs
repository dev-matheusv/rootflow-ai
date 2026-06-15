using RootFlow.Domain.Training;

namespace RootFlow.Application.Training.Dtos;

public sealed record TrainingProgramDto(
    Guid Id,
    Guid WorkspaceId,
    string Name,
    string Slug,
    string? Description,
    int PassingScore,
    bool IsPublished,
    Guid CreatedByUserId,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record TrainingProgramDetailDto(
    TrainingProgramDto Program,
    IReadOnlyList<TrainingModuleDto> Modules);

public sealed record TrainingModuleDto(
    Guid Id,
    Guid ProgramId,
    int OrderIndex,
    string Title,
    string? Description,
    IReadOnlyList<Guid> SourceDocumentIds,
    int QuestionCount,
    int PublishedQuestionCount,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);

public sealed record TrainingQuestionDto(
    Guid Id,
    Guid ModuleId,
    int OrderIndex,
    string Prompt,
    TrainingQuestionType Type,
    IReadOnlyList<string> Options,
    IReadOnlyList<int> CorrectAnswerIndices,
    string? Explanation,
    Guid? SourceDocumentId,
    Guid? SourceChunkId,
    TrainingQuestionStatus Status,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
