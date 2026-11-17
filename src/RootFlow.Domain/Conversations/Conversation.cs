namespace RootFlow.Domain.Conversations;

public sealed class Conversation
{
    private const string DefaultTitle = "Conversation";

    private Conversation()
    {
    }

    public Conversation(Guid id, Guid workspaceId, string? title, DateTime createdAtUtc, Guid? createdByUserId = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Conversation id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        Id = id;
        WorkspaceId = workspaceId;
        Title = NormalizeTitle(title);
        CreatedAtUtc = createdAtUtc;
        UpdatedAtUtc = createdAtUtc;
        CreatedByUserId = createdByUserId == Guid.Empty ? null : createdByUserId;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Title { get; private set; } = DefaultTitle;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    /// <summary>Who started the conversation. Null only for conversations created before RF-18.</summary>
    public Guid? CreatedByUserId { get; private set; }

    /// <summary>
    /// RF-18: a conversation is private to the person who started it. Conversations from before
    /// RF-18 have no owner and are visible only to workspace Owners and Admins.
    /// </summary>
    public bool IsVisibleTo(Guid userId, bool isWorkspaceAdmin)
    {
        return CreatedByUserId.HasValue
            ? userId != Guid.Empty && CreatedByUserId.Value == userId
            : isWorkspaceAdmin;
    }

    public void Rename(string? title)
    {
        Title = NormalizeTitle(title);
    }

    public void Touch(DateTime updatedAtUtc)
    {
        UpdatedAtUtc = updatedAtUtc;
    }

    private static string NormalizeTitle(string? title)
    {
        return string.IsNullOrWhiteSpace(title)
            ? DefaultTitle
            : title.Trim();
    }
}
