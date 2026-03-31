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

        if (evaluationCase.ExpectedTopDocument is not null)
        {
            Assert.NotEmpty(payload.Debug!.RetrievedChunks);
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
            Assert.Empty(payload.Debug!.RetrievedChunks);
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

    [Theory]
    [InlineData("What should I eat for breakfast?", "Greek yogurt with berries")]
    [InlineData("What should I eat for lunch?", "Grilled chicken bowl")]
    [InlineData("What should I eat for dinner?", "Baked salmon with quinoa")]
    [InlineData("O que devo comer no almoço?", "Grilled chicken bowl")]
    public async Task ChatFlow_AnswersDietQuestions_WithoutExplicitSourceReference(
        string question,
        string expectedAnswerFragment)
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "diet-plan.md", """
            # Weekly Diet Plan

            Breakfast: Greek yogurt with berries and almonds.
            Lunch: Grilled chicken bowl with brown rice, black beans, and avocado.
            Dinner: Baked salmon with quinoa and roasted vegetables.
            Snacks: Apple slices with peanut butter.
            """);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question,
            maxContextChunks = 8
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Sources);
        Assert.Equal("diet-plan.md", payload.Sources[0].DocumentName);
        Assert.Contains(expectedAnswerFragment, payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I do not know", payload.Answer, StringComparison.OrdinalIgnoreCase);
        AssertStructuredAnswer(payload.Answer);
        AssertSourcesAlignWithCitations(payload);

        if (question.Contains("O que", StringComparison.Ordinal))
        {
            Assert.Contains("- Recomendacao:", payload.Answer, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("- Recommendation:", payload.Answer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ChatFlow_RetrievesResumeSkills_ForGenericSkillsQuestion()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "resume.md", """
            # Resume

            Candidate: Ana Martins.
            Skills: C#, .NET, PostgreSQL, React, Docker, and Azure.
            Experience: Built customer portals, internal tools, and data APIs for operations teams.
            """);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question = "What skills does the candidate have?",
            maxContextChunks = 8
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Sources);
        Assert.Equal("resume.md", payload.Sources[0].DocumentName);
        Assert.Contains("C#", payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("React", payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I do not know", payload.Answer, StringComparison.OrdinalIgnoreCase);
        AssertStructuredAnswer(payload.Answer);
        AssertSourcesAlignWithCitations(payload);
        Assert.Contains("- Skills:", payload.Answer, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("What training should I do today?", "Lower-body strength session")]
    [InlineData("Qual treino devo fazer hoje?", "Lower-body strength session")]
    public async Task ChatFlow_AnswersTrainingQuestions_WithoutExplicitSourceReference(
        string question,
        string expectedAnswerFragment)
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "training-plan.md", """
            # Training Plan

            Today: Lower-body strength session with squats, lunges, and Romanian deadlifts.
            Wednesday: Upper-body workout with rows, presses, and pull-downs.
            Friday: Cardio intervals and core training.
            """);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question,
            maxContextChunks = 8
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Sources);
        Assert.Equal("training-plan.md", payload.Sources[0].DocumentName);
        Assert.Contains(expectedAnswerFragment, payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I do not know", payload.Answer, StringComparison.OrdinalIgnoreCase);
        AssertStructuredAnswer(payload.Answer);
        AssertSourcesAlignWithCitations(payload);

        if (question.Contains("Qual treino", StringComparison.Ordinal))
        {
            Assert.Contains("- Treino:", payload.Answer, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("- Training:", payload.Answer, StringComparison.Ordinal);
        }
    }

    [Theory]
    [InlineData("Based on my resume, what are my professional experiences?", "Contoso", "Fabrikam")]
    [InlineData("Com base no meu currículo, quais são minhas experiências profissionais?", "Contoso", "Fabrikam")]
    public async Task ChatFlow_RetrievesResumeExperiences_ForExplicitResumeQuestions(
        string question,
        string expectedCompanyOne,
        string expectedCompanyTwo)
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "resume.md", """
            # Resume

            Professional Experience:
            - Senior Software Engineer at Contoso from 2023 to 2025, leading internal platform delivery.
            - Software Engineer at Fabrikam from 2021 to 2023, building APIs and workflow automation.

            Education:
            - B.Sc. in Computer Science from UFRJ.
            """);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question,
            maxContextChunks = 8
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Sources);
        Assert.Equal("resume.md", payload.Sources[0].DocumentName);
        Assert.Contains(expectedCompanyOne, payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedCompanyTwo, payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I do not know", payload.Answer, StringComparison.OrdinalIgnoreCase);
        AssertStructuredAnswer(payload.Answer);
        AssertSourcesAlignWithCitations(payload);

        if (question.Contains("curr", StringComparison.OrdinalIgnoreCase))
        {
            Assert.Contains("- Experiencia profissional:", payload.Answer, StringComparison.Ordinal);
        }
        else
        {
            Assert.Contains("- Professional experience:", payload.Answer, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task ChatFlow_ReconstructsTrainingFollowUp_FromShortContextMessage()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "training-plan.md", """
            # Training Plan

            Monday: Lower-body strength session with squats, lunges, and Romanian deadlifts.
            Wednesday: Upper-body workout with rows, presses, and pull-downs.
            Friday: Cardio intervals and core training.
            """);

        var firstResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            question = "Qual treino devo fazer hoje?",
            maxContextChunks = 8
        });

        firstResponse.EnsureSuccessStatusCode();
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(firstPayload);

        var secondResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            conversationId = firstPayload!.ConversationId,
            question = "Hoje e segunda-feira",
            maxContextChunks = 8
        });

        secondResponse.EnsureSuccessStatusCode();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(secondPayload);
        Assert.NotNull(secondPayload!.Debug);
        Assert.Equal("training-plan.md", secondPayload.Sources[0].DocumentName);
        Assert.Contains("Lower-body strength session", secondPayload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("considerando que", secondPayload.Debug!.RetrievalQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("segunda-feira", secondPayload.Debug.RetrievalQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("- Treino:", secondPayload.Answer, StringComparison.Ordinal);
        AssertStructuredAnswer(secondPayload.Answer);
        AssertSourcesAlignWithCitations(secondPayload);
    }

    [Fact]
    public async Task ChatFlow_ReconstructsResumeFollowUp_ForEducationQuestion()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "resume.md", """
            # Resume

            Professional Experience:
            - Senior Software Engineer at Contoso from 2023 to 2025.

            Education:
            - B.Sc. in Computer Science from UFRJ.
            - Postgraduate certificate in Software Architecture.
            """);

        var firstResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            question = "Com base no meu curriculo, quais sao minhas experiencias profissionais?",
            maxContextChunks = 8
        });

        firstResponse.EnsureSuccessStatusCode();
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(firstPayload);

        var secondResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            conversationId = firstPayload!.ConversationId,
            question = "E a minha formacao?",
            maxContextChunks = 8
        });

        secondResponse.EnsureSuccessStatusCode();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(secondPayload);
        Assert.NotNull(secondPayload!.Debug);
        Assert.Equal("resume.md", secondPayload.Sources[0].DocumentName);
        Assert.Contains("UFRJ", secondPayload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("formacao", secondPayload.Debug!.RetrievalQuery, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("- Formacao:", secondPayload.Answer, StringComparison.Ordinal);
        AssertStructuredAnswer(secondPayload.Answer);
        AssertSourcesAlignWithCitations(secondPayload);
    }

    [Theory]
    [InlineData("Qual e a minha formacao academica?", "UFRJ", "- Formacao:")]
    [InlineData("What is my academic background?", "UFRJ", "- Education:")]
    public async Task ChatFlow_RetrievesEducation_ForSemanticEducationQueries(
        string question,
        string expectedFragment,
        string expectedSectionLabel)
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "resume.md", """
            # Resume

            Education:
            - B.Sc. in Computer Science from UFRJ.
            - Postgraduate certificate in Software Architecture.

            Skills:
            - C#, .NET, PostgreSQL, React.
            """);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question,
            maxContextChunks = 8
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(payload);
        Assert.NotNull(payload!.Debug);
        Assert.Equal("resume.md", payload.Sources[0].DocumentName);
        Assert.Contains(expectedFragment, payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedSectionLabel, payload.Answer, StringComparison.Ordinal);
        AssertStructuredAnswer(payload.Answer);
        AssertSourcesAlignWithCitations(payload);
    }

    [Theory]
    [InlineData("What is my current company?", "Contoso", "- Current company:")]
    [InlineData("Qual e minha empresa atual?", "Contoso", "- Empresa atual:")]
    public async Task ChatFlow_RetrievesCurrentCompany_ForResumeQuestion(
        string question,
        string expectedCompany,
        string expectedSectionLabel)
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "resume.md", """
            # Resume

            Current Company: Contoso.
            Professional Experience:
            - Senior Software Engineer at Contoso from 2023 to 2025.
            - Software Engineer at Fabrikam from 2021 to 2023.
            """);

        var response = await client.PostAsJsonAsync("/api/chat", new
        {
            question,
            maxContextChunks = 8
        });

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(payload);
        Assert.NotEmpty(payload!.Sources);
        Assert.Equal("resume.md", payload.Sources[0].DocumentName);
        Assert.Contains(expectedCompany, payload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Contains(expectedSectionLabel, payload.Answer, StringComparison.Ordinal);
        AssertStructuredAnswer(payload.Answer);
        AssertSourcesAlignWithCitations(payload);
    }

    [Fact]
    public async Task ChatFlow_SeparatesMixedDocuments_ForGenericQuestions()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "diet-plan.md", """
            # Weekly Diet Plan

            Breakfast: Greek yogurt with berries and almonds.
            Lunch: Grilled chicken bowl with brown rice, black beans, and avocado.
            Dinner: Baked salmon with quinoa and roasted vegetables.
            """);

        await UploadMarkdownAsync(client, "resume.md", """
            # Resume

            Candidate: Ana Martins.
            Skills: C#, .NET, PostgreSQL, React, Docker, and Azure.
            Experience: Led backend and frontend delivery for internal platforms.
            """);

        await UploadMarkdownAsync(client, "product-roadmap.md", """
            # Product Roadmap

            The mobile launch is scheduled for 15 May 2026.
            Beta feedback closes on 30 April 2026.
            """);

        var scenarios = new[]
        {
            new
            {
                Question = "What should I eat for lunch?",
                ExpectedDocument = "diet-plan.md",
                ExpectedAnswerFragment = "Grilled chicken bowl"
            },
            new
            {
                Question = "What skills does the candidate have?",
                ExpectedDocument = "resume.md",
                ExpectedAnswerFragment = "PostgreSQL"
            },
            new
            {
                Question = "When is the mobile launch scheduled?",
                ExpectedDocument = "product-roadmap.md",
                ExpectedAnswerFragment = "15 May 2026"
            }
        };

        foreach (var scenario in scenarios)
        {
            var response = await client.PostAsJsonAsync("/api/chat", new
            {
                question = scenario.Question,
                maxContextChunks = 8
            });

            response.EnsureSuccessStatusCode();
            var payload = await response.Content.ReadFromJsonAsync<ChatResponse>();

            Assert.NotNull(payload);
            Assert.NotEmpty(payload!.Sources);
            Assert.Equal(scenario.ExpectedDocument, payload.Sources[0].DocumentName);
            Assert.Contains(scenario.ExpectedAnswerFragment, payload.Answer, StringComparison.OrdinalIgnoreCase);
            Assert.DoesNotContain("I do not know", payload.Answer, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task ChatFlow_KeepsFreshRetrievalAcrossMixedDocumentConversation()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        await UploadMarkdownAsync(client, "diet-plan.md", """
            # Weekly Diet Plan

            Lunch: Grilled chicken bowl with brown rice, black beans, and avocado.
            """);

        await UploadMarkdownAsync(client, "training-plan.md", """
            # Training Plan

            Today: Lower-body strength session with squats, lunges, and Romanian deadlifts.
            """);

        await UploadMarkdownAsync(client, "resume.md", """
            # Resume

            Professional Experience:
            - Senior Software Engineer at Contoso from 2023 to 2025.
            """);

        var firstResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            question = "What should I eat for lunch?",
            maxContextChunks = 8
        });
        firstResponse.EnsureSuccessStatusCode();
        var firstPayload = await firstResponse.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(firstPayload);
        Assert.Equal("diet-plan.md", firstPayload!.Sources[0].DocumentName);
        Assert.Contains("Grilled chicken bowl", firstPayload.Answer, StringComparison.OrdinalIgnoreCase);
        AssertSourcesAlignWithCitations(firstPayload);

        var secondResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            conversationId = firstPayload.ConversationId,
            question = "Based on my resume, what are my professional experiences?",
            maxContextChunks = 8
        });
        secondResponse.EnsureSuccessStatusCode();
        var secondPayload = await secondResponse.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(secondPayload);
        Assert.Equal("resume.md", secondPayload!.Sources[0].DocumentName);
        Assert.Contains("Contoso", secondPayload.Answer, StringComparison.OrdinalIgnoreCase);
        AssertSourcesAlignWithCitations(secondPayload);

        var thirdResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            conversationId = firstPayload.ConversationId,
            question = "What training should I do today?",
            maxContextChunks = 8
        });
        thirdResponse.EnsureSuccessStatusCode();
        var thirdPayload = await thirdResponse.Content.ReadFromJsonAsync<ChatResponse>();

        Assert.NotNull(thirdPayload);
        Assert.Equal("training-plan.md", thirdPayload!.Sources[0].DocumentName);
        Assert.Contains("Lower-body strength session", thirdPayload.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("I do not know", thirdPayload.Answer, StringComparison.OrdinalIgnoreCase);
        AssertSourcesAlignWithCitations(thirdPayload);
    }

    private static void AssertStructuredAnswer(string answer)
    {
        var normalized = answer
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n');

        Assert.Contains("\n\n- ", normalized, StringComparison.Ordinal);
        Assert.Contains("\n  - ", normalized, StringComparison.Ordinal);
    }

    private static void AssertSourcesAlignWithCitations(ChatResponse payload)
    {
        Assert.NotNull(payload.Debug);
        Assert.NotNull(payload.Debug!.UsedChunkRanks);
        Assert.NotEmpty(payload.Debug.UsedChunkRanks);
        Assert.Equal(payload.Debug.UsedChunkRanks.Count, payload.Sources.Count);

        for (var index = 0; index < payload.Debug.UsedChunkRanks.Count; index++)
        {
            var citedRank = payload.Debug.UsedChunkRanks[index];
            var retrievedChunk = payload.Debug.RetrievedChunks[citedRank - 1];
            var source = payload.Sources[index];

            Assert.Equal(retrievedChunk.ChunkId, source.ChunkId);
            Assert.Equal(retrievedChunk.DocumentName, source.DocumentName);
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
        string RetrievalQuery,
        int HistoryMessageCount,
        int RetrievedChunkCount,
        List<int> UsedChunkRanks,
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
        double PhraseScore,
        List<string> MatchedTerms,
        string Reason);
}
