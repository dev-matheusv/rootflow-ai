using System.Net;
using System.Net.Http.Json;
using RootFlow.Api.IntegrationTests.Infrastructure;

namespace RootFlow.Api.IntegrationTests.Smoke;

public sealed class RootFlowApiSmokeTests : IClassFixture<RootFlowApiFactory>
{
    private readonly RootFlowApiFactory _factory;

    public RootFlowApiSmokeTests(RootFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task HealthCheck_ReturnsHealthy()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        var response = await client.GetAsync("/health");

        response.EnsureSuccessStatusCode();
        var payload = await response.Content.ReadFromJsonAsync<HealthResponse>();

        Assert.NotNull(payload);
        Assert.Equal("healthy", payload!.Status);
    }

    [Fact]
    public async Task MainApiFlow_WorksEndToEnd()
    {
        await _factory.ResetStateAsync();
        using var client = await _factory.CreateAuthenticatedClientAsync();

        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                """
                RootFlow helps businesses answer questions from internal knowledge.
                It stores uploaded content, retrieves relevant context, and returns grounded responses.
                """
            ))),
            "file",
            "rootflow-overview.txt");

        var uploadResponse = await client.PostAsync("/api/documents", multipart);
        if (!uploadResponse.IsSuccessStatusCode)
        {
            var body = await uploadResponse.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Upload failed with status {(int)uploadResponse.StatusCode}: {body}");
        }

        var createdDocument = await uploadResponse.Content.ReadFromJsonAsync<DocumentResponse>();
        Assert.NotNull(createdDocument);
        Assert.NotEqual(Guid.Empty, createdDocument!.Id);
        Assert.Equal(HttpStatusCode.Created, uploadResponse.StatusCode);

        var listResponse = await client.GetAsync("/api/documents");
        listResponse.EnsureSuccessStatusCode();

        var documents = await listResponse.Content.ReadFromJsonAsync<List<DocumentResponse>>();
        Assert.NotNull(documents);
        Assert.Single(documents!);

        var chatResponse = await client.PostAsJsonAsync("/api/chat", new
        {
            question = "How does RootFlow answer questions?",
            maxContextChunks = 3
        });
        if (!chatResponse.IsSuccessStatusCode)
        {
            var body = await chatResponse.Content.ReadAsStringAsync();
            throw new Xunit.Sdk.XunitException(
                $"Chat failed with status {(int)chatResponse.StatusCode}: {body}");
        }

        var chat = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chat);
        Assert.NotEqual(Guid.Empty, chat!.ConversationId);
        Assert.Contains("RootFlow helps businesses answer questions", chat.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.NotEmpty(chat.Sources);

        var historyResponse = await client.GetAsync($"/api/conversations/{chat.ConversationId}");
        historyResponse.EnsureSuccessStatusCode();

        var history = await historyResponse.Content.ReadFromJsonAsync<ConversationHistoryResponse>();
        Assert.NotNull(history);
        Assert.Equal(chat.ConversationId, history!.ConversationId);
        Assert.Equal(2, history.Messages.Count);
        Assert.Equal("How does RootFlow answer questions?", history.Messages[0].Content);
        Assert.Contains("RootFlow helps businesses answer questions", history.Messages[1].Content, StringComparison.OrdinalIgnoreCase);

        var conversationsResponse = await client.GetAsync("/api/conversations");
        conversationsResponse.EnsureSuccessStatusCode();

        var conversations = await conversationsResponse.Content.ReadFromJsonAsync<List<ConversationSummaryResponse>>();
        Assert.NotNull(conversations);
        Assert.Single(conversations!);
        Assert.Equal(chat.ConversationId, conversations[0].ConversationId);
        Assert.Equal(2, conversations[0].MessageCount);
        Assert.Equal("How does RootFlow answer questions?", conversations[0].Title);
        Assert.Contains("RootFlow helps businesses answer questions", conversations[0].LastMessagePreview, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task BillingFoundation_ExposesSeededPlans_AndWorkspaceCreditSummary()
    {
        await _factory.ResetStateAsync();
        using var client = await _factory.CreateAuthenticatedClientAsync();

        var session = await client.GetFromJsonAsync<SessionResponse>("/api/auth/me");
        Assert.NotNull(session);

        var plansResponse = await client.GetAsync("/api/billing/plans");
        plansResponse.EnsureSuccessStatusCode();

        var plans = await plansResponse.Content.ReadFromJsonAsync<List<BillingPlanResponse>>();
        Assert.NotNull(plans);
        Assert.Equal(3, plans!.Count);
        Assert.Contains(plans, plan => string.Equals(plan.Code, "starter", StringComparison.OrdinalIgnoreCase));

        var summaryResponse = await client.GetAsync($"/api/workspaces/{session!.Workspace.Id}/billing/summary");
        summaryResponse.EnsureSuccessStatusCode();

        var summary = await summaryResponse.Content.ReadFromJsonAsync<WorkspaceBillingSummaryResponse>();
        Assert.NotNull(summary);
        Assert.False(summary!.IsDegraded);
        Assert.NotNull(summary.BillingPlan);
        Assert.Equal("starter", summary.BillingPlan!.Code);
        Assert.NotNull(summary.Subscription);
        Assert.Equal("Trial", summary.Subscription!.Status);
        Assert.NotNull(summary.Subscription.TrialEndsAtUtc);
        Assert.Equal(5_000, summary.Balance.AvailableCredits);
        Assert.Equal(0, summary.Balance.ConsumedCredits);
    }

    private sealed record HealthResponse(string Status);

    private sealed record DocumentResponse(Guid Id, Guid WorkspaceId, string OriginalFileName);

    private sealed record SessionResponse(AuthUserResponse User, AuthWorkspaceResponse Workspace, string Role);

    private sealed record AuthUserResponse(Guid Id, string FullName, string Email);

    private sealed record AuthWorkspaceResponse(Guid Id, string Name, string Slug);

    private sealed record ChatResponse(Guid ConversationId, string Answer, string? ModelName, List<ChatSourceResponse> Sources);

    private sealed record ChatSourceResponse(Guid DocumentId, Guid ChunkId, string DocumentName, string Content, double Score);

    private sealed record ConversationHistoryResponse(Guid ConversationId, Guid WorkspaceId, string Title, List<ConversationMessageResponse> Messages);

    private sealed record ConversationMessageResponse(Guid Id, int Role, string Content, string? ModelName, DateTime CreatedAtUtc);

    private sealed record ConversationSummaryResponse(
        Guid ConversationId,
        string Title,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        int MessageCount,
        string? LastMessagePreview);

    private sealed record BillingPlanResponse(
        Guid Id,
        string Code,
        string Name,
        decimal MonthlyPrice,
        string CurrencyCode,
        long IncludedCredits,
        int MaxUsers,
        bool IsActive);

    private sealed record WorkspaceBillingSummaryResponse(
        string? CurrentPlanName,
        string? SubscriptionStatus,
        DateTime? TrialEndsAtUtc,
        bool IsDegraded,
        BillingPlanResponse? BillingPlan,
        WorkspaceSubscriptionResponse? Subscription,
        WorkspaceCreditBalanceResponse Balance);

    private sealed record WorkspaceSubscriptionResponse(
        Guid Id,
        Guid WorkspaceId,
        Guid BillingPlanId,
        string Status,
        DateTime CurrentPeriodStartUtc,
        DateTime CurrentPeriodEndUtc,
        DateTime? TrialEndsAtUtc,
        DateTime? CanceledAtUtc,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc);

    private sealed record WorkspaceCreditBalanceResponse(
        Guid WorkspaceId,
        long AvailableCredits,
        long ConsumedCredits,
        DateTime UpdatedAtUtc);
}
