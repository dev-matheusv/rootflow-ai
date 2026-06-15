using RootFlow.Application.Abstractions.AI;
using RootFlow.Domain.Training;

namespace RootFlow.Infrastructure.AI;

// Deterministic stub used in tests and AI=Fake mode. Returns one TrueFalse and
// one SingleChoice anchored to the first source chunk, regardless of count.
public sealed class FakeTrainingQuizGenerator : ITrainingQuizGenerator
{
    public Task<IReadOnlyList<GeneratedQuizQuestion>> GenerateAsync(
        TrainingQuizGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (request.SourceChunks.Count == 0)
        {
            return Task.FromResult<IReadOnlyList<GeneratedQuizQuestion>>([]);
        }

        var first = request.SourceChunks[0];
        IReadOnlyList<GeneratedQuizQuestion> result =
        [
            new GeneratedQuizQuestion(
                $"Verdadeiro ou falso: o documento '{first.DocumentName}' aborda o tema do módulo '{request.ModuleTitle}'?",
                TrainingQuestionType.TrueFalse,
                ["Verdadeiro", "Falso"],
                [0],
                "Resposta sintética para testes.",
                first.DocumentId,
                first.ChunkId),
            new GeneratedQuizQuestion(
                $"Qual é o principal tema abordado no módulo '{request.ModuleTitle}'?",
                TrainingQuestionType.SingleChoice,
                ["Conteúdo do módulo", "Tema não relacionado", "Outra coisa", "Nada"],
                [0],
                "Resposta sintética para testes.",
                first.DocumentId,
                first.ChunkId),
        ];

        return Task.FromResult(result);
    }
}
