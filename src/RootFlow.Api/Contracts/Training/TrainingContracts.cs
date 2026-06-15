using RootFlow.Domain.Training;

namespace RootFlow.Api.Contracts.Training;

// ── Requests ────────────────────────────────────────────────────────────

public sealed record CreateTrainingProgramRequest(
    string Name,
    string? Slug,
    string? Description);

public sealed record UpdateTrainingProgramRequest(
    string Name,
    string? Description,
    int PassingScore);

public sealed record AddTrainingModuleRequest(
    string Title,
    string? Description,
    int OrderIndex,
    IReadOnlyList<Guid> SourceDocumentIds);

public sealed record UpdateTrainingModuleRequest(
    string Title,
    string? Description,
    int OrderIndex,
    IReadOnlyList<Guid> SourceDocumentIds);

public sealed record GenerateTrainingQuizRequest(int QuestionCount);

public sealed record UpdateTrainingQuestionRequest(
    string Prompt,
    TrainingQuestionType Type,
    IReadOnlyList<string> Options,
    IReadOnlyList<int> CorrectAnswerIndices,
    string? Explanation);

// ── Responses ───────────────────────────────────────────────────────────

public sealed record TrainingProgramResponse(
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

public sealed record TrainingProgramDetailResponse(
    TrainingProgramResponse Program,
    IReadOnlyList<TrainingModuleResponse> Modules);

public sealed record TrainingModuleResponse(
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

public sealed record TrainingQuestionResponse(
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
