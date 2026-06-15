using RootFlow.Domain.Training;

namespace RootFlow.Application.Training.Dtos;

// View of a program as seen by the consumer (employee). Includes the
// employee's latest progress on each module so the UI can show resume/retry CTAs.
public sealed record AvailableTrainingProgramDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int PassingScore,
    int ModuleCount,
    int PassedModuleCount,
    DateTime UpdatedAtUtc);

public sealed record AvailableTrainingProgramDetailDto(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int PassingScore,
    IReadOnlyList<ConsumerModuleDto> Modules);

public sealed record ConsumerModuleDto(
    Guid Id,
    int OrderIndex,
    string Title,
    string? Description,
    int QuestionCount,
    ConsumerModuleStatus Status,
    int? LatestScore,
    DateTime? LastAttemptedAtUtc);

public enum ConsumerModuleStatus
{
    NotStarted,
    InProgress,
    Failed,
    Passed,
}

// Question as returned to the consumer mid-attempt: correctness intentionally hidden.
public sealed record ConsumerQuestionDto(
    Guid Id,
    int OrderIndex,
    string Prompt,
    TrainingQuestionType Type,
    IReadOnlyList<string> Options);

public sealed record StartAttemptResultDto(
    Guid AttemptId,
    Guid ModuleId,
    int PassingScore,
    DateTime StartedAtUtc,
    IReadOnlyList<ConsumerQuestionDto> Questions);

public sealed record AttemptResultDto(
    Guid AttemptId,
    Guid ModuleId,
    Guid ProgramId,
    TrainingAttemptStatus Status,
    int Score,
    int PassingScore,
    DateTime? CompletedAtUtc,
    int CorrectAnswerCount,
    int TotalQuestionCount);
