using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Training.Commands;
using RootFlow.Application.Training.Dtos;
using RootFlow.Domain.Training;

namespace RootFlow.Application.Training;

public sealed class TrainingConsumerService
{
    private readonly ITrainingRepository _trainingRepository;
    private readonly IClock _clock;
    private readonly ILogger<TrainingConsumerService> _logger;

    public TrainingConsumerService(
        ITrainingRepository trainingRepository,
        IClock clock,
        ILogger<TrainingConsumerService> logger)
    {
        _trainingRepository = trainingRepository;
        _clock = clock;
        _logger = logger;
    }

    public async Task<IReadOnlyList<AvailableTrainingProgramDto>> ListAvailableProgramsAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var programs = await _trainingRepository.ListProgramsByWorkspaceAsync(workspaceId, publishedOnly: true, cancellationToken);

        var results = new List<AvailableTrainingProgramDto>(programs.Count);
        foreach (var program in programs)
        {
            var modules = await _trainingRepository.ListModulesByProgramAsync(program.Id, cancellationToken);
            var passedCount = 0;
            foreach (var module in modules)
            {
                var attempts = await _trainingRepository.ListAttemptsByUserAndModuleAsync(userId, module.Id, cancellationToken);
                if (attempts.Any(a => a.Status == TrainingAttemptStatus.Passed))
                {
                    passedCount++;
                }
            }

            results.Add(new AvailableTrainingProgramDto(
                program.Id,
                program.Name,
                program.Slug,
                program.Description,
                program.PassingScore,
                modules.Count,
                passedCount,
                program.UpdatedAtUtc));
        }

        return results;
    }

    public async Task<AvailableTrainingProgramDetailDto> GetProgramDetailAsync(
        Guid programId,
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var program = await _trainingRepository.GetProgramByIdAsync(programId, workspaceId, cancellationToken);
        if (program is null || !program.IsPublished)
        {
            throw new TrainingNotFoundException($"Training program {programId} was not found.");
        }

        var modules = await _trainingRepository.ListModulesByProgramAsync(programId, cancellationToken);
        var moduleDtos = new List<ConsumerModuleDto>(modules.Count);

        foreach (var module in modules)
        {
            var publishedQuestions = await _trainingRepository.ListQuestionsByModuleAsync(module.Id, publishedOnly: true, cancellationToken);
            var attempts = await _trainingRepository.ListAttemptsByUserAndModuleAsync(userId, module.Id, cancellationToken);
            var latest = attempts.FirstOrDefault(); // already sorted DESC by startedAt
            var hasPassed = attempts.Any(a => a.Status == TrainingAttemptStatus.Passed);

            var status = hasPassed
                ? ConsumerModuleStatus.Passed
                : latest?.Status switch
                {
                    TrainingAttemptStatus.InProgress => ConsumerModuleStatus.InProgress,
                    TrainingAttemptStatus.Failed => ConsumerModuleStatus.Failed,
                    _ => ConsumerModuleStatus.NotStarted,
                };

            moduleDtos.Add(new ConsumerModuleDto(
                module.Id,
                module.OrderIndex,
                module.Title,
                module.Description,
                publishedQuestions.Count,
                status,
                latest?.Score,
                latest?.StartedAtUtc));
        }

        return new AvailableTrainingProgramDetailDto(
            program.Id,
            program.Name,
            program.Slug,
            program.Description,
            program.PassingScore,
            moduleDtos);
    }

    public async Task<StartAttemptResultDto> StartAttemptAsync(
        StartTrainingAttemptCommand command,
        CancellationToken cancellationToken = default)
    {
        var (module, program) = await RequireModuleForConsumerAsync(command.ModuleId, command.WorkspaceId, cancellationToken);

        if (!program.IsPublished)
        {
            throw new TrainingNotFoundException($"Training module {command.ModuleId} is not available.");
        }

        var questions = await _trainingRepository.ListQuestionsByModuleAsync(command.ModuleId, publishedOnly: true, cancellationToken);
        if (questions.Count == 0)
        {
            throw new InvalidOperationException("This module has no published questions yet.");
        }

        var attempt = new TrainingAttempt(
            Guid.NewGuid(),
            command.ModuleId,
            command.UserId,
            command.WorkspaceId,
            _clock.UtcNow);

        await _trainingRepository.AddAttemptAsync(attempt, cancellationToken);
        _logger.LogInformation(
            "Started training attempt {AttemptId} for user {UserId} on module {ModuleId}.",
            attempt.Id, command.UserId, module.Id);

        var consumerQuestions = questions
            .Select(q => new ConsumerQuestionDto(q.Id, q.OrderIndex, q.Prompt, q.Type, q.Options))
            .ToList();

        return new StartAttemptResultDto(
            attempt.Id,
            module.Id,
            program.PassingScore,
            attempt.StartedAtUtc,
            consumerQuestions);
    }

    public async Task SubmitAnswerAsync(
        SubmitTrainingAnswerCommand command,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _trainingRepository.GetAttemptByIdAsync(command.AttemptId, cancellationToken);
        if (attempt is null || attempt.UserId != command.UserId)
        {
            throw new TrainingNotFoundException($"Training attempt {command.AttemptId} was not found.");
        }

        if (attempt.Status != TrainingAttemptStatus.InProgress)
        {
            throw new InvalidOperationException("Cannot submit answers to a completed attempt.");
        }

        var question = await _trainingRepository.GetQuestionByIdAsync(command.QuestionId, cancellationToken);
        if (question is null || question.ModuleId != attempt.ModuleId)
        {
            throw new TrainingNotFoundException($"Training question {command.QuestionId} was not found in this attempt.");
        }

        var isCorrect = question.IsAnswerCorrect(command.SelectedIndices);
        var answer = new TrainingAnswer(
            Guid.NewGuid(),
            attempt.Id,
            question.Id,
            command.SelectedIndices,
            isCorrect);

        await _trainingRepository.AddAnswersAsync([answer], cancellationToken);
    }

    public async Task<AttemptResultDto> SubmitAttemptAsync(
        SubmitTrainingAttemptCommand command,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _trainingRepository.GetAttemptByIdAsync(command.AttemptId, cancellationToken);
        if (attempt is null || attempt.UserId != command.UserId)
        {
            throw new TrainingNotFoundException($"Training attempt {command.AttemptId} was not found.");
        }

        if (attempt.Status != TrainingAttemptStatus.InProgress)
        {
            // Idempotent: returning the previous result is friendlier than throwing.
            return await BuildAttemptResultAsync(attempt, cancellationToken);
        }

        var module = await _trainingRepository.GetModuleByIdAsync(attempt.ModuleId, cancellationToken)
            ?? throw new InvalidOperationException("The module backing this attempt no longer exists.");

        var program = await _trainingRepository.GetProgramByIdAsync(module.ProgramId, attempt.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException("The program backing this attempt no longer exists.");

        var publishedQuestions = await _trainingRepository.ListQuestionsByModuleAsync(attempt.ModuleId, publishedOnly: true, cancellationToken);
        var answers = await _trainingRepository.ListAnswersByAttemptAsync(attempt.Id, cancellationToken);

        // Questions that didn't get an answer count as wrong.
        var correctCount = publishedQuestions.Count(q =>
            answers.FirstOrDefault(a => a.QuestionId == q.Id)?.IsCorrect == true);
        var totalCount = publishedQuestions.Count;
        var score = totalCount == 0 ? 0 : (int)Math.Round(100.0 * correctCount / totalCount);

        attempt.Complete(score, program.PassingScore, _clock.UtcNow);
        await _trainingRepository.UpdateAttemptAsync(attempt, cancellationToken);

        _logger.LogInformation(
            "Completed training attempt {AttemptId} for user {UserId} on module {ModuleId}. Score: {Score}/{Passing}. Status: {Status}.",
            attempt.Id, attempt.UserId, attempt.ModuleId, score, program.PassingScore, attempt.Status);

        return new AttemptResultDto(
            attempt.Id,
            attempt.ModuleId,
            program.Id,
            attempt.Status,
            score,
            program.PassingScore,
            attempt.CompletedAtUtc,
            correctCount,
            totalCount);
    }

    public async Task<AttemptResultDto> GetAttemptResultAsync(
        Guid attemptId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var attempt = await _trainingRepository.GetAttemptByIdAsync(attemptId, cancellationToken);
        if (attempt is null || attempt.UserId != userId)
        {
            throw new TrainingNotFoundException($"Training attempt {attemptId} was not found.");
        }

        return await BuildAttemptResultAsync(attempt, cancellationToken);
    }

    private async Task<AttemptResultDto> BuildAttemptResultAsync(
        TrainingAttempt attempt,
        CancellationToken cancellationToken)
    {
        var module = await _trainingRepository.GetModuleByIdAsync(attempt.ModuleId, cancellationToken)
            ?? throw new InvalidOperationException("Module not found.");
        var program = await _trainingRepository.GetProgramByIdAsync(module.ProgramId, attempt.WorkspaceId, cancellationToken)
            ?? throw new InvalidOperationException("Program not found.");
        var publishedQuestions = await _trainingRepository.ListQuestionsByModuleAsync(attempt.ModuleId, publishedOnly: true, cancellationToken);
        var answers = await _trainingRepository.ListAnswersByAttemptAsync(attempt.Id, cancellationToken);
        var correct = publishedQuestions.Count(q =>
            answers.FirstOrDefault(a => a.QuestionId == q.Id)?.IsCorrect == true);

        return new AttemptResultDto(
            attempt.Id,
            attempt.ModuleId,
            program.Id,
            attempt.Status,
            attempt.Score ?? 0,
            program.PassingScore,
            attempt.CompletedAtUtc,
            correct,
            publishedQuestions.Count);
    }

    private async Task<(TrainingModule Module, TrainingProgram Program)> RequireModuleForConsumerAsync(
        Guid moduleId,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        var module = await _trainingRepository.GetModuleByIdAsync(moduleId, cancellationToken)
            ?? throw new TrainingNotFoundException($"Training module {moduleId} was not found.");
        var program = await _trainingRepository.GetProgramByIdAsync(module.ProgramId, workspaceId, cancellationToken)
            ?? throw new TrainingNotFoundException($"Training module {moduleId} was not found.");
        return (module, program);
    }
}
