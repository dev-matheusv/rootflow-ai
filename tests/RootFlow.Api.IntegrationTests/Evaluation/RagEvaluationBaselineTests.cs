using System.Net.Http.Json;
using RootFlow.Api.IntegrationTests.Infrastructure;

namespace RootFlow.Api.IntegrationTests.Evaluation;

public sealed class RagEvaluationBaselineTests : IClassFixture<RootFlowApiFactory>
{
    private readonly RootFlowApiFactory _factory;

    public RagEvaluationBaselineTests(RootFlowApiFactory factory)
    {
        _factory = factory;
    }

    public static TheoryData<EvaluationCase> EvaluationCases =>
        new(RagEvaluationDataset.Cases.ToArray());

    [Theory]
    [MemberData(nameof(EvaluationCases))]
    public async Task ChatFlow_RetrievesExpectedSource_AndReturnsGroundedAnswer(EvaluationCase evaluationCase)
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await SeedCorpusAsync(client);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question = evaluationCase.Question,
            maxContextChunks = 3
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(payload);
        Assert.NotNull(payload!.Debug);
        Assert.NotEmpty(payload.Debug!.RetrievedChunks);

        if (evaluationCase.ExpectedTopDocument is not null)
        {
            Assert.NotEmpty(payload.Sources);
            Assert.Equal(evaluationCase.ExpectedTopDocument, payload.Sources[0].DocumentName);
            Assert.Equal(evaluationCase.ExpectedTopDocument, payload.Debug.RetrievedChunks[0].DocumentName);
        }

        Assert.Contains(
            evaluationCase.ExpectedAnswerFragment,
            payload.Answer,
            StringComparison.OrdinalIgnoreCase);

        if (evaluationCase.ExpectedCitationMarker is not null)
        {
            Assert.Contains(
                evaluationCase.ExpectedCitationMarker,
                payload.Answer,
                StringComparison.Ordinal);
        }
        else
        {
            Assert.DoesNotContain("[1]", payload.Answer, StringComparison.Ordinal);
        }

        if (evaluationCase.ExpectInsufficientContext)
        {
            Assert.Empty(payload.Sources);
        }
    }

    private static async Task SeedCorpusAsync(HttpClient client)
    {
        foreach (var document in RagEvaluationDataset.Documents)
        {
            await using var stream = File.OpenRead(document.Path);
            using var multipart = new MultipartFormDataContent();
            multipart.Add(new StreamContent(stream), "file", document.FileName);

            var response = await client.PostAsync("/api/documents", multipart);
            response.EnsureSuccessStatusCode();
        }
    }

    private sealed record ChatResponse(
        Guid ConversationId,
        string Answer,
        string? ModelName,
        List<ChatSourceResponse> Sources,
        ChatDebugResponse? Debug);

    private sealed record ChatSourceResponse(
        Guid DocumentId,
        Guid ChunkId,
        string DocumentName,
        string SourceLabel,
        string Content,
        double Score);

    private sealed record ChatDebugResponse(
        string Query,
        int HistoryMessageCount,
        int RetrievedChunkCount,
        List<RetrievedChunkDebugResponse> RetrievedChunks);

    private sealed record RetrievedChunkDebugResponse(
        int Rank,
        Guid ChunkId,
        string DocumentName,
        string SourceLabel,
        int Sequence,
        double Score,
        double VectorScore,
        double KeywordScore,
        List<string> MatchedTerms,
        string Reason);
}
