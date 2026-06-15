using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Training.Commands;
using RootFlow.Application.Training.Dtos;
using RootFlow.Domain.Training;

namespace RootFlow.Application.Training;

public sealed class TrainingAuthoringService
{
    private const int MaxChunksForQuiz = 20;

    private readonly ITrainingRepository _trainingRepository;
    private readonly IKnowledgeDocumentRepository _documentRepository;
    private readonly IDocumentChunkRepository _chunkRepository;
    private readonly ITrainingQuizGenerator _quizGenerator;
    private readonly IClock _clock;
    private readonly ILogger<TrainingAuthoringService> _logger;

    public TrainingAuthoringService(
        ITrainingRepository trainingRepository,
        IKnowledgeDocumentRepository documentRepository,
        IDocumentChunkRepository chunkRepository,
        ITrainingQuizGenerator quizGenerator,
        IClock clock,
        ILogger<TrainingAuthoringService> logger)
    {
        _trainingRepository = trainingRepository;
        _documentRepository = documentRepository;
        _chunkRepository = chunkRepository;
        _quizGenerator = quizGenerator;
        _clock = clock;
        _logger = logger;
    }

    // ─────────────────────────────────────────────────────────────────────
    // Programs
    // ─────────────────────────────────────────────────────────────────────

    public async Task<TrainingProgramDto> CreateProgramAsync(
        CreateTrainingProgramCommand command,
        CancellationToken cancellationToken = default)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Name);

        var slug = NormalizeSlug(command.Slug ?? command.Name);
        if (await _trainingRepository.ProgramSlugExistsAsync(slug, command.WorkspaceId, cancellationToken))
        {
            throw new ArgumentException($"A program with slug '{slug}' already exists.", nameof(command.Slug));
        }

        var program = new TrainingProgram(
            Guid.NewGuid(),
            command.WorkspaceId,
            command.Name,
            slug,
            command.Description,
            command.CreatedByUserId,
            _clock.UtcNow);

        await _trainingRepository.AddProgramAsync(program, cancellationToken);
        _logger.LogInformation("Created training program {ProgramId} in workspace {WorkspaceId}.", program.Id, program.WorkspaceId);

        return MapProgram(program);
    }

    public async Task<TrainingProgramDto> UpdateProgramAsync(
        UpdateTrainingProgramCommand command,
        CancellationToken cancellationToken = default)
    {
        var program = await RequireProgramAsync(command.ProgramId, command.WorkspaceId, cancellationToken);
        program.UpdateDetails(command.Name, command.Description, command.PassingScore, _clock.UtcNow);
        await _trainingRepository.UpdateProgramAsync(program, cancellationToken);
        return MapProgram(program);
    }

    public async Task<TrainingProgramDto> PublishProgramAsync(
        PublishTrainingProgramCommand command,
        CancellationToken cancellationToken = default)
    {
        var program = await RequireProgramAsync(command.ProgramId, command.WorkspaceId, cancellationToken);

        var modules = await _trainingRepository.ListModulesByProgramAsync(command.ProgramId, cancellationToken);
        if (modules.Count == 0)
        {
            throw new TrainingPublishValidationException("A program must have at least one module before being published.");
        }

        foreach (var module in modules)
        {
            var publishedQuestions = await _trainingRepository.ListQuestionsByModuleAsync(module.Id, publishedOnly: true, cancellationToken);
            if (publishedQuestions.Count < 3)
            {
                throw new TrainingPublishValidationException(
                    $"Module '{module.Title}' must have at least 3 published questions before the program can be published.");
            }
        }

        program.Publish(_clock.UtcNow);
        await _trainingRepository.UpdateProgramAsync(program, cancellationToken);
        _logger.LogInformation("Published training program {ProgramId} in workspace {WorkspaceId}.", program.Id, program.WorkspaceId);
        return MapProgram(program);
    }

    public async Task<TrainingProgramDto> UnpublishProgramAsync(
        Guid programId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var program = await RequireProgramAsync(programId, workspaceId, cancellationToken);
        program.Unpublish(_clock.UtcNow);
        await _trainingRepository.UpdateProgramAsync(program, cancellationToken);
        return MapProgram(program);
    }

    public async Task<IReadOnlyList<TrainingProgramDto>> ListProgramsAsync(
        Guid workspaceId,
        bool publishedOnly,
        CancellationToken cancellationToken = default)
    {
        var programs = await _trainingRepository.ListProgramsByWorkspaceAsync(workspaceId, publishedOnly, cancellationToken);
        return programs.Select(MapProgram).ToList();
    }

    public async Task<TrainingProgramDetailDto> GetProgramDetailAsync(
        Guid programId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        var program = await RequireProgramAsync(programId, workspaceId, cancellationToken);
        var modules = await _trainingRepository.ListModulesByProgramAsync(programId, cancellationToken);

        var moduleDtos = new List<TrainingModuleDto>(modules.Count);
        foreach (var module in modules)
        {
            var allQuestions = await _trainingRepository.ListQuestionsByModuleAsync(module.Id, publishedOnly: false, cancellationToken);
            var publishedCount = allQuestions.Count(q => q.Status == TrainingQuestionStatus.Published);
            moduleDtos.Add(MapModule(module, allQuestions.Count, publishedCount));
        }

        return new TrainingProgramDetailDto(MapProgram(program), moduleDtos);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Modules
    // ─────────────────────────────────────────────────────────────────────

    public async Task<TrainingModuleDto> AddModuleAsync(
        AddTrainingModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var program = await RequireProgramAsync(command.ProgramId, command.WorkspaceId, cancellationToken);
        ArgumentException.ThrowIfNullOrWhiteSpace(command.Title);

        var module = new TrainingModule(
            Guid.NewGuid(),
            program.Id,
            command.OrderIndex,
            command.Title,
            command.Description,
            command.SourceDocumentIds,
            _clock.UtcNow);

        await _trainingRepository.AddModuleAsync(module, cancellationToken);
        return MapModule(module, questionCount: 0, publishedQuestionCount: 0);
    }

    public async Task<TrainingModuleDto> UpdateModuleAsync(
        UpdateTrainingModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        var module = await RequireModuleInWorkspaceAsync(command.ModuleId, command.WorkspaceId, cancellationToken);
        module.UpdateDetails(command.Title, command.Description, command.OrderIndex, command.SourceDocumentIds, _clock.UtcNow);
        await _trainingRepository.UpdateModuleAsync(module, cancellationToken);

        var all = await _trainingRepository.ListQuestionsByModuleAsync(module.Id, publishedOnly: false, cancellationToken);
        return MapModule(module, all.Count, all.Count(q => q.Status == TrainingQuestionStatus.Published));
    }

    public async Task DeleteModuleAsync(
        DeleteTrainingModuleCommand command,
        CancellationToken cancellationToken = default)
    {
        await RequireModuleInWorkspaceAsync(command.ModuleId, command.WorkspaceId, cancellationToken);
        await _trainingRepository.DeleteModuleAsync(command.ModuleId, cancellationToken);
    }

    public async Task<IReadOnlyList<TrainingQuestionDto>> ListModuleQuestionsAsync(
        Guid moduleId,
        Guid workspaceId,
        bool publishedOnly,
        CancellationToken cancellationToken = default)
    {
        await RequireModuleInWorkspaceAsync(moduleId, workspaceId, cancellationToken);
        var questions = await _trainingRepository.ListQuestionsByModuleAsync(moduleId, publishedOnly, cancellationToken);
        return questions.Select(MapQuestion).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────
    // AI quiz generation
    // ─────────────────────────────────────────────────────────────────────

    public async Task<IReadOnlyList<TrainingQuestionDto>> GenerateQuizAsync(
        GenerateTrainingQuizCommand command,
        CancellationToken cancellationToken = default)
    {
        var module = await RequireModuleInWorkspaceAsync(command.ModuleId, command.WorkspaceId, cancellationToken);

        if (module.SourceDocumentIds.Count == 0)
        {
            throw new ArgumentException("Module has no source documents to generate a quiz from.", nameof(command.ModuleId));
        }

        // Collect chunks from each source document. We cap the total so the prompt stays bounded.
        var sourceChunks = new List<QuizSourceChunk>();
        foreach (var documentId in module.SourceDocumentIds)
        {
            if (sourceChunks.Count >= MaxChunksForQuiz) break;

            var document = await _documentRepository.GetByIdAsync(command.WorkspaceId, documentId, cancellationToken);
            if (document is null) continue;

            var chunks = await _chunkRepository.ListByDocumentIdAsync(command.WorkspaceId, documentId, cancellationToken);
            foreach (var chunk in chunks.Take(MaxChunksForQuiz - sourceChunks.Count))
            {
                sourceChunks.Add(new QuizSourceChunk(chunk.Id, document.Id, document.OriginalFileName, chunk.Content));
            }
        }

        if (sourceChunks.Count == 0)
        {
            throw new InvalidOperationException("No usable content chunks were found in the module's source documents.");
        }

        _logger.LogInformation(
            "Generating training quiz for module {ModuleId} from {ChunkCount} source chunks.",
            module.Id,
            sourceChunks.Count);

        var generated = await _quizGenerator.GenerateAsync(
            new TrainingQuizGenerationRequest(module.Title, module.Description, command.QuestionCount, sourceChunks),
            cancellationToken);

        var existing = await _trainingRepository.ListQuestionsByModuleAsync(module.Id, publishedOnly: false, cancellationToken);
        var nextOrder = existing.Count > 0 ? existing.Max(q => q.OrderIndex) + 1 : 0;

        var now = _clock.UtcNow;
        var questions = new List<TrainingQuestion>();
        foreach (var g in generated)
        {
            try
            {
                var question = new TrainingQuestion(
                    Guid.NewGuid(),
                    module.Id,
                    nextOrder++,
                    g.Prompt,
                    g.Type,
                    g.Options,
                    g.CorrectAnswerIndices,
                    g.Explanation,
                    g.SourceDocumentId,
                    g.SourceChunkId,
                    now);
                questions.Add(question);
            }
            catch (ArgumentException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Skipped AI-generated question that failed domain validation for module {ModuleId}.",
                    module.Id);
            }
        }

        if (questions.Count == 0)
        {
            throw new InvalidOperationException("The AI did not return any valid questions.");
        }

        await _trainingRepository.AddQuestionsBulkAsync(questions, cancellationToken);
        _logger.LogInformation(
            "Persisted {QuestionCount} AI-generated questions for module {ModuleId}.",
            questions.Count,
            module.Id);

        return questions.Select(MapQuestion).ToList();
    }

    // ─────────────────────────────────────────────────────────────────────
    // Questions
    // ─────────────────────────────────────────────────────────────────────

    public async Task<TrainingQuestionDto> UpdateQuestionAsync(
        UpdateTrainingQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        var question = await RequireQuestionInWorkspaceAsync(command.QuestionId, command.WorkspaceId, cancellationToken);
        question.UpdateContent(
            command.Prompt,
            command.Type,
            command.Options,
            command.CorrectAnswerIndices,
            command.Explanation,
            _clock.UtcNow);
        await _trainingRepository.UpdateQuestionAsync(question, cancellationToken);
        return MapQuestion(question);
    }

    public async Task<TrainingQuestionDto> PublishQuestionAsync(
        PublishTrainingQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        var question = await RequireQuestionInWorkspaceAsync(command.QuestionId, command.WorkspaceId, cancellationToken);
        question.Publish(_clock.UtcNow);
        await _trainingRepository.UpdateQuestionAsync(question, cancellationToken);
        return MapQuestion(question);
    }

    public async Task DeleteQuestionAsync(
        DeleteTrainingQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        await RequireQuestionInWorkspaceAsync(command.QuestionId, command.WorkspaceId, cancellationToken);
        await _trainingRepository.DeleteQuestionAsync(command.QuestionId, cancellationToken);
    }

    // ─────────────────────────────────────────────────────────────────────
    // Helpers
    // ─────────────────────────────────────────────────────────────────────

    private async Task<TrainingProgram> RequireProgramAsync(Guid programId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var program = await _trainingRepository.GetProgramByIdAsync(programId, workspaceId, cancellationToken);
        if (program is null)
        {
            throw new TrainingNotFoundException($"Training program {programId} was not found in workspace {workspaceId}.");
        }
        return program;
    }

    private async Task<TrainingModule> RequireModuleInWorkspaceAsync(Guid moduleId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var module = await _trainingRepository.GetModuleByIdAsync(moduleId, cancellationToken);
        if (module is null)
        {
            throw new TrainingNotFoundException($"Training module {moduleId} was not found.");
        }

        // Ensure the module belongs to a program in the caller's workspace.
        var program = await _trainingRepository.GetProgramByIdAsync(module.ProgramId, workspaceId, cancellationToken);
        if (program is null)
        {
            throw new TrainingNotFoundException($"Training module {moduleId} was not found.");
        }
        return module;
    }

    private async Task<TrainingQuestion> RequireQuestionInWorkspaceAsync(Guid questionId, Guid workspaceId, CancellationToken cancellationToken)
    {
        var question = await _trainingRepository.GetQuestionByIdAsync(questionId, cancellationToken);
        if (question is null)
        {
            throw new TrainingNotFoundException($"Training question {questionId} was not found.");
        }

        await RequireModuleInWorkspaceAsync(question.ModuleId, workspaceId, cancellationToken);
        return question;
    }

    private static string NormalizeSlug(string raw)
    {
        var lower = raw.Trim().ToLowerInvariant();
        var chars = lower.Select(c =>
            char.IsLetterOrDigit(c) ? c :
            c is ' ' or '-' or '_' ? '-' :
            (char?)null);
        var slug = string.Concat(chars.Where(c => c.HasValue).Select(c => c!.Value));
        while (slug.Contains("--"))
        {
            slug = slug.Replace("--", "-");
        }
        return slug.Trim('-');
    }

    private static TrainingProgramDto MapProgram(TrainingProgram program) =>
        new(
            program.Id,
            program.WorkspaceId,
            program.Name,
            program.Slug,
            program.Description,
            program.PassingScore,
            program.IsPublished,
            program.CreatedByUserId,
            program.CreatedAtUtc,
            program.UpdatedAtUtc);

    private static TrainingModuleDto MapModule(TrainingModule module, int questionCount, int publishedQuestionCount) =>
        new(
            module.Id,
            module.ProgramId,
            module.OrderIndex,
            module.Title,
            module.Description,
            module.SourceDocumentIds,
            questionCount,
            publishedQuestionCount,
            module.CreatedAtUtc,
            module.UpdatedAtUtc);

    private static TrainingQuestionDto MapQuestion(TrainingQuestion question) =>
        new(
            question.Id,
            question.ModuleId,
            question.OrderIndex,
            question.Prompt,
            question.Type,
            question.Options,
            question.CorrectAnswerIndices,
            question.Explanation,
            question.SourceDocumentId,
            question.SourceChunkId,
            question.Status,
            question.CreatedAtUtc,
            question.UpdatedAtUtc);
}
