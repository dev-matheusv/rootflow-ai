namespace RootFlow.Application.Chat;

public sealed class ConversationNotFoundException : Exception
{
    public ConversationNotFoundException()
        : base("Conversation was not found.")
    {
    }
}
