namespace RootFlow.Application.Conversations.Dtos;

public sealed record ConversationHistoryDto(
    Guid ConversationId,
    Guid WorkspaceId,
    string Title,
    IReadOnlyList<ConversationMessageDto> Messages);
