using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Domain;

public sealed class WorkspaceInvitationTests
{
    [Fact]
    public void Constructor_NormalizesEmailAndStartsPending()
    {
        var createdAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var expiresAtUtc = createdAtUtc.AddDays(7);

        var invitation = new WorkspaceInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            " Invitee@Example.com ",
            WorkspaceRole.Member,
            "invite-token",
            Guid.NewGuid(),
            createdAtUtc,
            expiresAtUtc);

        Assert.Equal("Invitee@Example.com", invitation.Email);
        Assert.Equal("INVITEE@EXAMPLE.COM", invitation.NormalizedEmail);
        Assert.Equal(WorkspaceInvitationStatus.Pending, invitation.Status);
        Assert.True(invitation.CanBeAccepted(createdAtUtc.AddDays(1)));
    }

    [Fact]
    public void Accept_StoresAcceptedState()
    {
        var createdAtUtc = new DateTime(2026, 4, 1, 0, 0, 0, DateTimeKind.Utc);
        var acceptedAtUtc = createdAtUtc.AddDays(1);

        var invitation = new WorkspaceInvitation(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "invitee@example.com",
            WorkspaceRole.Admin,
            "invite-token",
            Guid.NewGuid(),
            createdAtUtc,
            createdAtUtc.AddDays(7));

        invitation.Accept(acceptedAtUtc);

        Assert.Equal(WorkspaceInvitationStatus.Accepted, invitation.Status);
        Assert.Equal(acceptedAtUtc, invitation.AcceptedAtUtc);
    }
}
