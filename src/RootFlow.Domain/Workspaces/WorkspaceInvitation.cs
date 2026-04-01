using System.Net.Mail;

namespace RootFlow.Domain.Workspaces;

public sealed class WorkspaceInvitation
{
    private WorkspaceInvitation()
    {
    }

    public WorkspaceInvitation(
        Guid id,
        Guid workspaceId,
        string email,
        WorkspaceRole role,
        string token,
        Guid invitedByUserId,
        DateTime createdAtUtc,
        DateTime expiresAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Invitation id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (invitedByUserId == Guid.Empty)
        {
            throw new ArgumentException("Invited-by user id cannot be empty.", nameof(invitedByUserId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        if (expiresAtUtc <= createdAtUtc)
        {
            throw new ArgumentException("Invitation expiry must be after creation time.", nameof(expiresAtUtc));
        }

        Id = id;
        WorkspaceId = workspaceId;
        Email = NormalizeAndValidateEmail(email);
        NormalizedEmail = Email.ToUpperInvariant();
        Role = role;
        Token = token.Trim();
        InvitedByUserId = invitedByUserId;
        CreatedAtUtc = createdAtUtc;
        ExpiresAtUtc = expiresAtUtc;
        Status = WorkspaceInvitationStatus.Pending;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Email { get; private set; } = null!;

    public string NormalizedEmail { get; private set; } = null!;

    public WorkspaceRole Role { get; private set; }

    public string Token { get; private set; } = null!;

    public Guid InvitedByUserId { get; private set; }

    public WorkspaceInvitationStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime ExpiresAtUtc { get; private set; }

    public DateTime? AcceptedAtUtc { get; private set; }

    public DateTime? RevokedAtUtc { get; private set; }

    public bool CanBeAccepted(DateTime utcNow)
    {
        return Status == WorkspaceInvitationStatus.Pending && utcNow <= ExpiresAtUtc;
    }

    public void ChangeRole(WorkspaceRole role)
    {
        if (Status != WorkspaceInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending invitations can change roles.");
        }

        Role = role;
    }

    public void Accept(DateTime acceptedAtUtc)
    {
        if (Status != WorkspaceInvitationStatus.Pending)
        {
            throw new InvalidOperationException("Only pending invitations can be accepted.");
        }

        if (acceptedAtUtc > ExpiresAtUtc)
        {
            throw new InvalidOperationException("Expired invitations cannot be accepted.");
        }

        Status = WorkspaceInvitationStatus.Accepted;
        AcceptedAtUtc = acceptedAtUtc;
        RevokedAtUtc = null;
    }

    public void Revoke(DateTime revokedAtUtc)
    {
        if (Status == WorkspaceInvitationStatus.Accepted)
        {
            throw new InvalidOperationException("Accepted invitations cannot be revoked.");
        }

        if (Status == WorkspaceInvitationStatus.Revoked)
        {
            return;
        }

        Status = WorkspaceInvitationStatus.Revoked;
        RevokedAtUtc = revokedAtUtc;
    }

    public void Expire(DateTime expiredAtUtc)
    {
        if (Status != WorkspaceInvitationStatus.Pending)
        {
            return;
        }

        if (expiredAtUtc < ExpiresAtUtc)
        {
            throw new InvalidOperationException("Invitation expiry cannot be set before the configured expiration time.");
        }

        Status = WorkspaceInvitationStatus.Expired;
    }

    private static string NormalizeAndValidateEmail(string email)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(email);

        var trimmed = email.Trim();

        try
        {
            var address = new MailAddress(trimmed);
            return address.Address;
        }
        catch (FormatException exception)
        {
            throw new ArgumentException("Invitation email address is invalid.", nameof(email), exception);
        }
    }
}
