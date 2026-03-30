using System.Net.Http.Json;
using System.Text;
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

    [Fact]
    public async Task ChatFlow_UsesDifferentDocumentsForDifferentQuestions_WhenDocumentsShareLongBoilerplate()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        var documents = new[]
        {
            new
            {
                FileName = "travel-policy.md",
                Content = BuildLongBoilerplateDocument(
                    "Travel Policy",
                    "Flights longer than six hours may be booked in premium economy with manager approval."),
                Question = "What cabin class can be booked for flights longer than six hours?",
                ExpectedAnswerFragment = "premium economy"
            },
            new
            {
                FileName = "security-runbook.md",
                Content = BuildLongBoilerplateDocument(
                    "Security Runbook",
                    "Security incidents must be reported to the hotline at +55 11 4000-9000 immediately."),
                Question = "What hotline number should be used for a security incident?",
                ExpectedAnswerFragment = "+55 11 4000-9000"
            },
            new
            {
                FileName = "vendor-onboarding.md",
                Content = BuildLongBoilerplateDocument(
                    "Vendor Onboarding",
                    "Finance requires a signed W-8BEN or W-9 before the first vendor payment is released."),
                Question = "Which tax form does finance need before the first vendor payment?",
                ExpectedAnswerFragment = "W-8BEN or W-9"
            }
        };

        foreach (var document in documents)
        {
            await UploadMarkdownAsync(client, document.FileName, document.Content);
        }

        var retrievedDocuments = new List<string>();

        foreach (var document in documents)
        {
            var response = await client.PostAsJsonAsync("/api/chat", new
            {
                question = document.Question,
                maxContextChunks = 3
            });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

            Assert.NotNull(payload);
            Assert.NotEmpty(payload!.Sources);
            Assert.Equal(document.FileName, payload.Sources[0].DocumentName);
            Assert.Contains(document.ExpectedAnswerFragment, payload.Answer, StringComparison.OrdinalIgnoreCase);

            retrievedDocuments.Add(payload.Sources[0].DocumentName);
        }

        Assert.Equal(documents.Length, retrievedDocuments.Distinct(StringComparer.Ordinal).Count());
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

    private static async Task UploadMarkdownAsync(HttpClient client, string fileName, string content)
    {
        await using var stream = new MemoryStream(Encoding.UTF8.GetBytes(content));
        using var multipart = new MultipartFormDataContent();
        multipart.Add(new StreamContent(stream), "file", fileName);

        var response = await client.PostAsync("/api/documents", multipart);
        response.EnsureSuccessStatusCode();
    }

    private static string BuildLongBoilerplateDocument(string title, string specificSentence)
    {
        var boilerplate = string.Join(
            ' ',
            Enumerable.Repeat(
                "This operational guidance explains how RootFlow teams review internal policies, keep records current, and follow shared process standards across the company.",
                18));

        return $"""
                # {title}

                {boilerplate} {specificSentence}
                """;
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
