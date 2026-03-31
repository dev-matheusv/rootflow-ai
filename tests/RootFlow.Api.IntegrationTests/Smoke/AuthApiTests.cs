using System.Net;
using System.Net.Http.Json;
using RootFlow.Api.IntegrationTests.Infrastructure;

namespace RootFlow.Api.IntegrationTests.Smoke;

public sealed class AuthApiTests : IClassFixture<RootFlowApiFactory>
{
    private readonly RootFlowApiFactory _factory;

    public AuthApiTests(RootFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task ProtectedEndpoints_RequireAuthentication()
    {
        await _factory.ResetStateAsync();
        using var client = _factory.CreateApiClient();

        var response = await client.GetAsync("/api/documents");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Signup_And_Login_ReturnWorkspaceSession()
    {
        await _factory.ResetStateAsync();
        using var signupClient = _factory.CreateApiClient();

        var signupResponse = await signupClient.PostAsJsonAsync("/api/auth/signup", new
        {
            fullName = "Jordan Rivera",
            email = "jordan@rootflow.test",
            password = "Password123!",
            workspaceName = "Acme Operations"
        });

        signupResponse.EnsureSuccessStatusCode();

        var signupPayload = await signupResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(signupPayload);
        Assert.False(string.IsNullOrWhiteSpace(signupPayload!.Token));
        Assert.Equal("Jordan Rivera", signupPayload.Session.User.FullName);
        Assert.Equal("Owner", signupPayload.Session.Role);
        Assert.Equal("Acme Operations", signupPayload.Session.Workspace.Name);

        using var loginClient = _factory.CreateApiClient();
        var loginResponse = await loginClient.PostAsJsonAsync("/api/auth/login", new
        {
            email = "jordan@rootflow.test",
            password = "Password123!"
        });

        loginResponse.EnsureSuccessStatusCode();

        var loginPayload = await loginResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(loginPayload);
        Assert.Equal(signupPayload.Session.Workspace.Id, loginPayload!.Session.Workspace.Id);
        Assert.Equal(signupPayload.Session.User.Id, loginPayload.Session.User.Id);

        loginClient.DefaultRequestHeaders.Authorization =
            new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", loginPayload.Token);

        var meResponse = await loginClient.GetAsync("/api/auth/me");
        meResponse.EnsureSuccessStatusCode();

        var mePayload = await meResponse.Content.ReadFromJsonAsync<SessionResponse>();
        Assert.NotNull(mePayload);
        Assert.Equal("Owner", mePayload!.Role);
        Assert.Equal(signupPayload.Session.Workspace.Id, mePayload.Workspace.Id);
    }

    [Fact]
    public async Task WorkspaceIsolation_PreventsCrossTenantReads()
    {
        await _factory.ResetStateAsync();
        using var firstClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "First Owner",
            email: "owner-one@rootflow.test",
            workspaceName: "First Workspace");

        using var secondClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Second Owner",
            email: "owner-two@rootflow.test",
            workspaceName: "Second Workspace");

        using var multipart = new MultipartFormDataContent();
        multipart.Add(
            new StreamContent(new MemoryStream(System.Text.Encoding.UTF8.GetBytes(
                """
                RootFlow keeps each workspace isolated from all other tenants.
                """
            ))),
            "file",
            "isolation-check.txt");

        var uploadResponse = await firstClient.PostAsync("/api/documents", multipart);
        uploadResponse.EnsureSuccessStatusCode();

        var chatResponse = await firstClient.PostAsJsonAsync("/api/chat", new
        {
            question = "What does RootFlow keep isolated?",
            maxContextChunks = 3
        });
        chatResponse.EnsureSuccessStatusCode();

        var chatPayload = await chatResponse.Content.ReadFromJsonAsync<ChatResponse>();
        Assert.NotNull(chatPayload);

        var secondDocumentsResponse = await secondClient.GetAsync("/api/documents");
        secondDocumentsResponse.EnsureSuccessStatusCode();

        var secondDocuments = await secondDocumentsResponse.Content.ReadFromJsonAsync<List<DocumentResponse>>();
        Assert.NotNull(secondDocuments);
        Assert.Empty(secondDocuments!);

        var secondConversationsResponse = await secondClient.GetAsync("/api/conversations");
        secondConversationsResponse.EnsureSuccessStatusCode();

        var secondConversations = await secondConversationsResponse.Content.ReadFromJsonAsync<List<ConversationSummaryResponse>>();
        Assert.NotNull(secondConversations);
        Assert.Empty(secondConversations!);

        var crossTenantHistory = await secondClient.GetAsync($"/api/conversations/{chatPayload!.ConversationId}");
        Assert.Equal(HttpStatusCode.NotFound, crossTenantHistory.StatusCode);
    }

    private sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, SessionResponse Session);

    private sealed record SessionResponse(AuthUserResponse User, AuthWorkspaceResponse Workspace, string Role);

    private sealed record AuthUserResponse(Guid Id, string FullName, string Email);

    private sealed record AuthWorkspaceResponse(Guid Id, string Name, string Slug);

    private sealed record ChatResponse(Guid ConversationId, string Answer, string? ModelName, List<ChatSourceResponse> Sources);

    private sealed record ChatSourceResponse(Guid DocumentId, Guid ChunkId, string DocumentName, string Content, double Score);

    private sealed record DocumentResponse(Guid Id, Guid WorkspaceId, string OriginalFileName);

    private sealed record ConversationSummaryResponse(
        Guid ConversationId,
        string Title,
        DateTime CreatedAtUtc,
        DateTime UpdatedAtUtc,
        int MessageCount,
        string? LastMessagePreview);
}
