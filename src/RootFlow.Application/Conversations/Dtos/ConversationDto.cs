namespace RootFlow.Application.Conversations.Dtos;

public sealed record ConversationDto(
    Guid Id,
    Guid WorkspaceId,
    string Title,
    DateTime CreatedAtUtc,
    DateTime UpdatedAtUtc);
