namespace RootFlow.Application.Chat.Commands;

public sealed record AskQuestionCommand(
    Guid WorkspaceId,
    string Question,
    Guid? ConversationId = null,
    int MaxContextChunks = 8);
