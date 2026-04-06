namespace RootFlow.Domain.Billing;

public sealed class WorkspaceBillingNotificationDelivery
{
    private WorkspaceBillingNotificationDelivery()
    {
    }

    public WorkspaceBillingNotificationDelivery(
        Guid id,
        Guid? workspaceId,
        string notificationKind,
        string dedupeKey,
        string recipientEmail,
        DateTime sentAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Billing notification delivery id cannot be empty.", nameof(id));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(notificationKind);
        ArgumentException.ThrowIfNullOrWhiteSpace(dedupeKey);
        ArgumentException.ThrowIfNullOrWhiteSpace(recipientEmail);

        Id = id;
        WorkspaceId = workspaceId;
        NotificationKind = notificationKind.Trim();
        DedupeKey = dedupeKey.Trim();
        RecipientEmail = recipientEmail.Trim().ToLowerInvariant();
        SentAtUtc = sentAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid? WorkspaceId { get; private set; }

    public string NotificationKind { get; private set; } = null!;

    public string DedupeKey { get; private set; } = null!;

    public string RecipientEmail { get; private set; } = null!;

    public DateTime SentAtUtc { get; private set; }
}
