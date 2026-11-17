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

    // RF-11: demotion and removal take effect on the very next request, with the same (still valid) token.
    [Fact]
    public async Task OwnerDemotesAndRemovesMember_AccessChangesImmediately()
    {
        await _factory.ResetStateAsync();
        using var ownerClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Workspace Owner",
            email: "owner@rootflow.test",
            workspaceName: "Acme Legal");

        var ownerSession = await ownerClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");
        Assert.NotNull(ownerSession);
        var workspaceId = ownerSession!.Workspace.Id;

        (await ownerClient.PostAsJsonAsync($"/api/workspaces/{workspaceId}/invites", new
        {
            email = "lawyer@rootflow.test",
            role = "Admin"
        })).EnsureSuccessStatusCode();

        var notification = _factory.GetLatestWorkspaceInvitationNotification("lawyer@rootflow.test");
        Assert.NotNull(notification);

        using var lawyerClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Lawyer",
            email: "lawyer@rootflow.test",
            workspaceName: "Lawyer Personal");

        var acceptResponse = await lawyerClient.PostAsJsonAsync("/api/workspaces/invites/accept", new { token = notification!.Token });
        acceptResponse.EnsureSuccessStatusCode();
        var acceptPayload = await acceptResponse.Content.ReadFromJsonAsync<AuthResponse>();
        lawyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", acceptPayload!.Token);

        // As Admin the token can invite.
        (await lawyerClient.PostAsJsonAsync($"/api/workspaces/{workspaceId}/invites", new
        {
            email = "intern@rootflow.test",
            role = "Member"
        })).EnsureSuccessStatusCode();

        // Owner demotes to Member: the same token loses admin rights right away.
        var demoteResponse = await ownerClient.PatchAsJsonAsync(
            $"/api/workspaces/{workspaceId}/members/{acceptPayload.Session.User.Id}",
            new { role = "Member" });
        demoteResponse.EnsureSuccessStatusCode();
        var demoted = await demoteResponse.Content.ReadFromJsonAsync<WorkspaceMemberResponse>();
        Assert.Equal("Member", demoted!.Role);

        var forbiddenInvite = await lawyerClient.PostAsJsonAsync($"/api/workspaces/{workspaceId}/invites", new
        {
            email = "intern2@rootflow.test",
            role = "Member"
        });
        Assert.Equal(HttpStatusCode.Forbidden, forbiddenInvite.StatusCode);

        // Member cannot remove the owner.
        var removeOwner = await lawyerClient.DeleteAsync($"/api/workspaces/{workspaceId}/members/{ownerSession.User.Id}");
        Assert.Equal(HttpStatusCode.Forbidden, removeOwner.StatusCode);

        // Owner removes: the same token is rejected on the next request.
        var removeResponse = await ownerClient.DeleteAsync($"/api/workspaces/{workspaceId}/members/{acceptPayload.Session.User.Id}");
        Assert.Equal(HttpStatusCode.NoContent, removeResponse.StatusCode);

        var meAfterRemoval = await lawyerClient.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.Unauthorized, meAfterRemoval.StatusCode);

        var members = await ownerClient.GetFromJsonAsync<List<WorkspaceMemberResponse>>($"/api/workspaces/{workspaceId}/members");
        Assert.DoesNotContain(members!, candidate => candidate.Email == "lawyer@rootflow.test");
    }

    // RF-18: a member never sees, continues or deletes someone else's conversation, not even an admin.
    [Fact]
    public async Task Conversations_ArePrivateToTheirAuthor()
    {
        await _factory.ResetStateAsync();
        using var ownerClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Workspace Owner",
            email: "owner@rootflow.test",
            workspaceName: "Acme Legal");
        var ownerSession = await ownerClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");
        var workspaceId = ownerSession!.Workspace.Id;

        (await ownerClient.PostAsJsonAsync($"/api/workspaces/{workspaceId}/invites", new
        {
            email = "lawyer@rootflow.test",
            role = "Member"
        })).EnsureSuccessStatusCode();
        var notification = _factory.GetLatestWorkspaceInvitationNotification("lawyer@rootflow.test");
        using var lawyerClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Lawyer",
            email: "lawyer@rootflow.test",
            workspaceName: "Lawyer Personal");
        var accept = await lawyerClient.PostAsJsonAsync("/api/workspaces/invites/accept", new { token = notification!.Token });
        accept.EnsureSuccessStatusCode();
        var acceptPayload = await accept.Content.ReadFromJsonAsync<AuthResponse>();
        lawyerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", acceptPayload!.Token);

        // The member starts a conversation.
        var chat = await lawyerClient.PostAsJsonAsync("/api/chat", new { question = "Confidential question about client X" });
        chat.EnsureSuccessStatusCode();
        var chatPayload = await chat.Content.ReadFromJsonAsync<ChatIdResponse>();
        var conversationId = chatPayload!.ConversationId;

        var lawyerList = await lawyerClient.GetFromJsonAsync<List<ConversationIdResponse>>("/api/conversations");
        Assert.Contains(lawyerList!, c => c.ConversationId == conversationId);

        // The owner (admin rights) cannot list, read, continue or delete it.
        var ownerList = await ownerClient.GetFromJsonAsync<List<ConversationIdResponse>>("/api/conversations");
        Assert.DoesNotContain(ownerList!, c => c.ConversationId == conversationId);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.GetAsync($"/api/conversations/{conversationId}")).StatusCode);
        var hijack = await ownerClient.PostAsJsonAsync("/api/chat", new { question = "Append to it", conversationId });
        Assert.Equal(HttpStatusCode.NotFound, hijack.StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await ownerClient.DeleteAsync($"/api/conversations/{conversationId}")).StatusCode);

        // The author can delete their own conversation.
        Assert.Equal(HttpStatusCode.NoContent, (await lawyerClient.DeleteAsync($"/api/conversations/{conversationId}")).StatusCode);
    }

    // RF-13: export my data, then delete my account (LGPD art. 18).
    [Fact]
    public async Task DataSubject_CanExportAndDeleteAccount_WithSafeguards()
    {
        await _factory.ResetStateAsync();
        using var ownerClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Workspace Owner",
            email: "owner@rootflow.test",
            workspaceName: "Acme Legal");
        var ownerSession = await ownerClient.GetFromJsonAsync<SessionResponse>("/api/auth/me");

        (await ownerClient.PostAsJsonAsync($"/api/workspaces/{ownerSession!.Workspace.Id}/invites", new
        {
            email = "leaver@rootflow.test",
            role = "Member"
        })).EnsureSuccessStatusCode();
        var notification = _factory.GetLatestWorkspaceInvitationNotification("leaver@rootflow.test");

        using var leaverClient = await _factory.CreateAuthenticatedClientAsync(
            fullName: "Leaving Lawyer",
            email: "leaver@rootflow.test",
            workspaceName: "Leaver Personal");
        (await leaverClient.PostAsJsonAsync("/api/workspaces/invites/accept", new { token = notification!.Token })).EnsureSuccessStatusCode();

        // Export: a JSON file with the person's own data.
        var export = await leaverClient.GetAsync("/api/me/export");
        export.EnsureSuccessStatusCode();
        Assert.Equal("application/json", export.Content.Headers.ContentType!.MediaType);
        var exportBody = await export.Content.ReadAsStringAsync();
        Assert.Contains("leaver@rootflow.test", exportBody);
        Assert.Contains("Acme Legal", exportBody);
        Assert.DoesNotContain("owner@rootflow.test", exportBody);

        // Wrong password never deletes.
        var wrongPassword = await leaverClient.PostAsJsonAsync("/api/me/delete", new { password = "not-the-password" });
        Assert.Equal(HttpStatusCode.Forbidden, wrongPassword.StatusCode);

        // The owner of a workspace other people use cannot delete the account (would erase their company).
        var ownerDelete = await ownerClient.PostAsJsonAsync("/api/me/delete", new { password = "Password123!" });
        Assert.Equal(HttpStatusCode.Conflict, ownerDelete.StatusCode);

        // The member deletes: their solo personal workspace is closed with the account, sessions stop working.
        var delete = await leaverClient.PostAsJsonAsync("/api/me/delete", new { password = "Password123!" });
        Assert.Equal(HttpStatusCode.NoContent, delete.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await leaverClient.GetAsync("/api/auth/me")).StatusCode);

        using var anonymous = _factory.CreateClient();
        var login = await anonymous.PostAsJsonAsync("/api/auth/login", new { email = "leaver@rootflow.test", password = "Password123!" });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);

        var members = await ownerClient.GetFromJsonAsync<List<WorkspaceMemberResponse>>($"/api/workspaces/{ownerSession.Workspace.Id}/members");
        Assert.DoesNotContain(members!, m => m.Email == "leaver@rootflow.test");
    }

    private sealed record ChatIdResponse(Guid ConversationId);

    private sealed record ConversationIdResponse(Guid ConversationId);

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
