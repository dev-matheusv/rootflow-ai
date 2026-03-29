namespace RootFlow.Application.Conversations.Dtos;

public sealed record ConversationSummaryDto(
    Guid ConversationId,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc,
    int MessageCount,
    string? LastMessagePreview);
