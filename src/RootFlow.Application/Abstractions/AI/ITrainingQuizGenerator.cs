using RootFlow.Domain.Training;

namespace RootFlow.Application.Abstractions.AI;

public interface ITrainingQuizGenerator
{
    Task<IReadOnlyList<GeneratedQuizQuestion>> GenerateAsync(
        TrainingQuizGenerationRequest request,
        CancellationToken cancellationToken = default);
}

public sealed record TrainingQuizGenerationRequest(
    string ModuleTitle,
    string? ModuleDescription,
    int RequestedQuestionCount,
    IReadOnlyList<QuizSourceChunk> SourceChunks);

public sealed record QuizSourceChunk(
    Guid ChunkId,
    Guid DocumentId,
    string DocumentName,
    string Text);

public sealed record GeneratedQuizQuestion(
    string Prompt,
    TrainingQuestionType Type,
    IReadOnlyList<string> Options,
    IReadOnlyList<int> CorrectAnswerIndices,
    string Explanation,
    Guid? SourceDocumentId,
    Guid? SourceChunkId);
