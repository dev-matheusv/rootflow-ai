namespace RootFlow.Domain.Conversations;

public sealed class Conversation
{
    private const string DefaultTitle = "Conversation";

    private Conversation()
    {
    }

    public Conversation(Guid id, Guid workspaceId, string? title, DateTime createdAtUtc)
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
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string Title { get; private set; } = DefaultTitle;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

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
