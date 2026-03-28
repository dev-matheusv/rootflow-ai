namespace RootFlow.Domain.Conversations;

public sealed class ConversationMessage
{
    private ConversationMessage()
    {
    }

    public ConversationMessage(
        Guid id,
        Guid conversationId,
        MessageRole role,
        string content,
        DateTime createdAtUtc,
        string? modelName = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Message id cannot be empty.", nameof(id));
        }

        if (conversationId == Guid.Empty)
        {
            throw new ArgumentException("Conversation id cannot be empty.", nameof(conversationId));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);

        Id = id;
        ConversationId = conversationId;
        Role = role;
        Content = content.Trim();
        ModelName = string.IsNullOrWhiteSpace(modelName) ? null : modelName.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid ConversationId { get; private set; }

    public MessageRole Role { get; private set; }

    public string Content { get; private set; } = null!;

    public string? ModelName { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
