using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using RootFlow.Api.IntegrationTests.Infrastructure;

namespace RootFlow.Api.IntegrationTests.Smoke;

public sealed class WorkspaceInvitesApiTests : IClassFixture<RootFlowApiFactory>
{
    private readonly RootFlowApiFactory _factory;

    public WorkspaceInvitesApiTests(RootFlowApiFactory factory)
    {
        _factory = factory;
    }

    [Fact]
    public async Task OwnerCanInviteUser_AndInviteeCanAcceptIntoWorkspace()
    {
        await _factory.ResetStateAsync();
        using var ownerClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Workspace Owner",
            email: "owner@rootflow.test",
            workspaceName: "Acme Ops");

        var ownerSession = await ownerClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");
        Assert.NotNull(ownerSession);

        var inviteResponse = await ownerClient.PostAsJsonAsync($"/api/workspaces/{ownerSession!.Workspace.Id}/invites", new
        {
            email = "invitee@rootflow.test",
            role = "Admin"
        });
        inviteResponse.EnsureSuccessStatusCode();

        var notification = _factory.GetLatestWorkspaceInvitationNotification("invitee@rootflow.test");
        Assert.NotNull(notification);

        using var inviteeClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Invited Admin",
            email: "invitee@rootflow.test",
            workspaceName: "Invitee Personal");

        var acceptResponse = await inviteeClient.PostAsJsonAsync("/api/workspaces/invites/accept", new
        {
            token = notification!.Token
        });
        acceptResponse.EnsureSuccessStatusCode();

        var acceptPayload = await acceptResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(acceptPayload);
        Assert.Equal(ownerSession.Workspace.Id, acceptPayload!.Session.Workspace.Id);
        Assert.Equal("Admin", acceptPayload.Session.Role);

        inviteeClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", acceptPayload.Token);
        var meResponse = await inviteeClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");
        Assert.NotNull(meResponse);
        Assert.Equal(ownerSession.Workspace.Id, meResponse!.Workspace.Id);

        var membersResponse = await ownerClient.GetFromJsonAsync<List<WorkspaceMemberResponse>>($"/api/workspaces/{ownerSession.Workspace.Id}/members");
        Assert.NotNull(membersResponse);
        Assert.Contains(membersResponse!, candidate => candidate.Email == "owner@rootflow.test" && candidate.Role == "Owner");
        Assert.Contains(membersResponse!, candidate => candidate.Email == "invitee@rootflow.test" && candidate.Role == "Admin");
    }

    [Fact]
    public async Task MemberCannotInviteAdditionalUsers()
    {
        await _factory.ResetStateAsync();
        using var ownerClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Workspace Owner",
            email: "owner@rootflow.test",
            workspaceName: "Acme Ops");

        var ownerSession = await ownerClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");
        Assert.NotNull(ownerSession);

        var ownerInviteResponse = await ownerClient.PostAsJsonAsync($"/api/workspaces/{ownerSession!.Workspace.Id}/invites", new
        {
            email = "member@rootflow.test",
            role = "Member"
        });
        ownerInviteResponse.EnsureSuccessStatusCode();

        var notification = _factory.GetLatestWorkspaceInvitationNotification("member@rootflow.test");
        Assert.NotNull(notification);

        using var memberClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Workspace Member",
            email: "member@rootflow.test",
            workspaceName: "Member Personal");

        var acceptResponse = await memberClient.PostAsJsonAsync("/api/workspaces/invites/accept", new
        {
            token = notification!.Token
        });
        acceptResponse.EnsureSuccessStatusCode();

        var acceptPayload = await acceptResponse.Content.ReadFromJsonAsync<AuthResponse>();
        Assert.NotNull(acceptPayload);
        memberClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", acceptPayload!.Token);

        var forbiddenResponse = await memberClient.PostAsJsonAsync($"/api/workspaces/{ownerSession.Workspace.Id}/invites", new
        {
            email = "another@rootflow.test",
            role = "Member"
        });

        Assert.Equal(HttpStatusCode.Forbidden, forbiddenResponse.StatusCode);
    }

    private sealed record AuthResponse(string Token, DateTime ExpiresAtUtc, SessionResponse Session);

    private sealed record SessionResponse(AuthUserResponse User, AuthWorkspaceResponse Workspace, string Role);

    private sealed record AuthUserResponse(Guid Id, string FullName, string Email);

    private sealed record AuthWorkspaceResponse(Guid Id, string Name, string Slug);

    private sealed record WorkspaceMemberResponse(
        Guid UserId,
        string FullName,
        string Email,
        string Role,
        DateTime CreatedAtUtc,
        bool IsCurrentUser);
}
