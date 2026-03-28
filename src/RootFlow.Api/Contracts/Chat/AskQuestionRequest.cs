namespace RootFlow.Api.Contracts.Chat;

public sealed record AskQuestionRequest(
    string Question,
    Guid? ConversationId = null,
    int MaxContextChunks = 5);
