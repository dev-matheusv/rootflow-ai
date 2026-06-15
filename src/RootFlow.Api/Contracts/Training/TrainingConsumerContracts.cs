using RootFlow.Application.Training.Dtos;
using RootFlow.Domain.Training;

namespace RootFlow.Api.Contracts.Training;

public sealed record SubmitTrainingAnswerRequest(
    Guid QuestionId,
    IReadOnlyList<int> SelectedIndices);

public sealed record AvailableTrainingProgramResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int PassingScore,
    int ModuleCount,
    int PassedModuleCount,
    DateTime UpdatedAtUtc);

public sealed record AvailableTrainingProgramDetailResponse(
    Guid Id,
    string Name,
    string Slug,
    string? Description,
    int PassingScore,
    IReadOnlyList<ConsumerModuleResponse> Modules);

public sealed record ConsumerModuleResponse(
    Guid Id,
    int OrderIndex,
    string Title,
    string? Description,
    int QuestionCount,
    ConsumerModuleStatus Status,
    int? LatestScore,
    DateTime? LastAttemptedAtUtc);

public sealed record ConsumerQuestionResponse(
    Guid Id,
    int OrderIndex,
    string Prompt,
    TrainingQuestionType Type,
    IReadOnlyList<string> Options);

public sealed record StartAttemptResponse(
    Guid AttemptId,
    Guid ModuleId,
    int PassingScore,
    DateTime StartedAtUtc,
    IReadOnlyList<ConsumerQuestionResponse> Questions);

public sealed record AttemptResultResponse(
    Guid AttemptId,
    Guid ModuleId,
    Guid ProgramId,
    TrainingAttemptStatus Status,
    int Score,
    int PassingScore,
    DateTime? CompletedAtUtc,
    int CorrectAnswerCount,
    int TotalQuestionCount);
