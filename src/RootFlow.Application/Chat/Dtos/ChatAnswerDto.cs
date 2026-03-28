namespace RootFlow.Application.Chat.Dtos;

public sealed record ChatAnswerDto(
    Guid ConversationId,
    string Answer,
    string? ModelName,
    IReadOnlyList<ChatSourceDto> Sources,
    ChatRagDebugDto? Debug = null);
