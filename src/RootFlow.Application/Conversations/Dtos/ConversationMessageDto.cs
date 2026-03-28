using RootFlow.Domain.Conversations;

namespace RootFlow.Application.Conversations.Dtos;

public sealed record ConversationMessageDto(
    Guid Id,
    MessageRole Role,
    string Content,
    string? ModelName,
    DateTime CreatedAtUtc);
