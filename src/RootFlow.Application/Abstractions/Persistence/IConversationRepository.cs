using RootFlow.Domain.Conversations;
using RootFlow.Application.Conversations.Dtos;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IConversationRepository
{
    Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default);

    Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default);

    /// <summary>Deletes the conversation and, by cascade, its messages. Returns false when not found in the workspace.</summary>
    Task<bool> DeleteAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default);

    Task<Conversation?> GetByIdAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(
        Guid workspaceId,
        Guid conversationId,
        CancellationToken cancellationToken = default);

    /// <summary>RF-18: the user's own conversations, plus unowned (pre RF-18) ones when <paramref name="includeUnowned"/>.</summary>
    Task<IReadOnlyList<ConversationSummaryDto>> ListSummariesAsync(
        Guid workspaceId,
        Guid userId,
        bool includeUnowned,
        CancellationToken cancellationToken = default);
}
