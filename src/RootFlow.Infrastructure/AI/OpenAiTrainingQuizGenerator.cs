using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Domain.Training;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.AI;

public sealed class OpenAiTrainingQuizGenerator : ITrainingQuizGenerator
{
    private readonly HttpClient _httpClient;
    private readonly OpenAiOptions _options;
    private readonly ILogger<OpenAiTrainingQuizGenerator> _logger;

    private static readonly JsonSerializerOptions ResponseJson = new()
    {
        PropertyNameCaseInsensitive = true,
    };

    public OpenAiTrainingQuizGenerator(
        HttpClient httpClient,
        IOptions<OpenAiOptions> options,
        ILogger<OpenAiTrainingQuizGenerator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public async Task<IReadOnlyList<GeneratedQuizQuestion>> GenerateAsync(
        TrainingQuizGenerationRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.ApiKey))
        {
            throw new InvalidOperationException("OpenAI API key is not configured.");
        }

        if (request.SourceChunks.Count == 0)
        {
            return [];
        }

        var requestedCount = Math.Clamp(request.RequestedQuestionCount, 3, 12);

        // The model receives indexed chunks so it can cite the source it used.
        var chunkBlocks = string.Join("\n\n", request.SourceChunks.Select((c, idx) =>
            $"[chunk {idx}] (documento: {c.DocumentName})\n{c.Text}"));

        var systemPrompt = """
            Você é um especialista em treinamento corporativo. Sua tarefa é gerar perguntas de
            avaliação a partir de trechos de documentos. As perguntas devem ser claras, objetivas,
            sempre ancoradas no conteúdo fornecido, e em português do Brasil — exceto se os
            trechos estiverem em outra língua, caso em que você responde na mesma língua.

            Regras obrigatórias:
            - Cada pergunta deve poder ser respondida APENAS com base nos trechos fornecidos.
            - Nunca invente fatos.
            - Para perguntas "SingleChoice": exatamente 4 opções e exatamente 1 correta.
            - Para perguntas "MultiChoice": 4 opções com 2 ou 3 corretas.
            - Para perguntas "TrueFalse": exatamente 2 opções ("Verdadeiro" e "Falso") com 1 correta.
            - Misture os tipos quando fizer sentido.
            - "explanation" deve referenciar brevemente o trecho usado.
            - "sourceChunkIndex" deve apontar pro [chunk N] que sustenta a pergunta.
            """;

        var userPrompt = $"""
            Módulo: {request.ModuleTitle}
            {(string.IsNullOrWhiteSpace(request.ModuleDescription) ? "" : $"Descrição: {request.ModuleDescription}")}

            Gere exatamente {requestedCount} perguntas a partir dos trechos abaixo:

            {chunkBlocks}
            """;

        var payload = new
        {
            model = _options.ChatModel,
            temperature = 0.3,
            messages = new[]
            {
                new { role = "system", content = systemPrompt },
                new { role = "user", content = userPrompt },
            },
            response_format = new
            {
                type = "json_schema",
                json_schema = new
                {
                    name = "TrainingQuiz",
                    strict = true,
                    schema = BuildJsonSchema(),
                },
            },
        };

        using var response = await _httpClient.PostAsJsonAsync("chat/completions", payload, cancellationToken);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            _logger.LogError(
                "OpenAI quiz generation failed with status {StatusCode}. Body: {Body}",
                (int)response.StatusCode,
                body);
            throw new InvalidOperationException(
                $"OpenAI quiz generation failed with status {(int)response.StatusCode}.");
        }

        await using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var document = await JsonDocument.ParseAsync(stream, cancellationToken: cancellationToken);

        var content = document.RootElement
            .GetProperty("choices")[0]
            .GetProperty("message")
            .GetProperty("content")
            .GetString();

        if (string.IsNullOrWhiteSpace(content))
        {
            throw new InvalidOperationException("OpenAI quiz response was empty.");
        }

        var parsed = JsonSerializer.Deserialize<QuizResponseDto>(content, ResponseJson)
            ?? throw new InvalidOperationException("OpenAI quiz response could not be parsed.");

        return parsed.Questions
            .Where(q => q.Options is { Count: >= 2 } && q.CorrectAnswerIndices is { Count: >= 1 })
            .Select(q =>
            {
                var sourceChunk = q.SourceChunkIndex is { } idx && idx >= 0 && idx < request.SourceChunks.Count
                    ? request.SourceChunks[idx]
                    : null;

                return new GeneratedQuizQuestion(
                    q.Prompt.Trim(),
                    ParseType(q.Type),
                    q.Options.Select(o => o.Trim()).ToList(),
                    q.CorrectAnswerIndices,
                    q.Explanation?.Trim() ?? string.Empty,
                    sourceChunk?.DocumentId,
                    sourceChunk?.ChunkId);
            })
            .ToList();
    }

    private static TrainingQuestionType ParseType(string raw)
    {
        return raw switch
        {
            "SingleChoice" => TrainingQuestionType.SingleChoice,
            "MultiChoice" => TrainingQuestionType.MultiChoice,
            "TrueFalse" => TrainingQuestionType.TrueFalse,
            _ => TrainingQuestionType.SingleChoice,
        };
    }

    private static object BuildJsonSchema()
    {
        return new
        {
            type = "object",
            additionalProperties = false,
            required = new[] { "questions" },
            properties = new
            {
                questions = new
                {
                    type = "array",
                    items = new
                    {
                        type = "object",
                        additionalProperties = false,
                        required = new[]
                        {
                            "prompt",
                            "type",
                            "options",
                            "correctAnswerIndices",
                            "explanation",
                            "sourceChunkIndex",
                        },
                        properties = new
                        {
                            prompt = new { type = "string" },
                            type = new
                            {
                                type = "string",
                                @enum = new[] { "SingleChoice", "MultiChoice", "TrueFalse" },
                            },
                            options = new
                            {
                                type = "array",
                                items = new { type = "string" },
                            },
                            correctAnswerIndices = new
                            {
                                type = "array",
                                items = new { type = "integer" },
                            },
                            explanation = new { type = "string" },
                            sourceChunkIndex = new { type = "integer" },
                        },
                    },
                },
            },
        };
    }

    private sealed record QuizResponseDto
    {
        [JsonPropertyName("questions")]
        public List<QuizQuestionDto> Questions { get; init; } = [];
    }

    private sealed record QuizQuestionDto
    {
        [JsonPropertyName("prompt")] public string Prompt { get; init; } = string.Empty;
        [JsonPropertyName("type")] public string Type { get; init; } = "SingleChoice";
        [JsonPropertyName("options")] public List<string> Options { get; init; } = [];
        [JsonPropertyName("correctAnswerIndices")] public List<int> CorrectAnswerIndices { get; init; } = [];
        [JsonPropertyName("explanation")] public string? Explanation { get; init; }
        [JsonPropertyName("sourceChunkIndex")] public int? SourceChunkIndex { get; init; }
    }
}
