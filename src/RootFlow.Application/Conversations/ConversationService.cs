using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Conversations.Dtos;
using RootFlow.Application.Conversations.Queries;

namespace RootFlow.Application.Conversations;

public sealed class ConversationService
{
    private readonly IConversationRepository _conversationRepository;

    public ConversationService(IConversationRepository conversationRepository)
    {
        _conversationRepository = conversationRepository;
    }

    /// <summary>
    /// RF-12/RF-18: deletes the conversation and its messages. Only whoever can see it can delete it:
    /// the person who started it, or an Owner/Admin for unowned (pre RF-18) conversations.
    /// </summary>
    public async Task<bool> DeleteAsync(
        Guid workspaceId,
        Guid conversationId,
        Guid userId,
        bool isWorkspaceAdmin,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(workspaceId, conversationId, cancellationToken);
        if (conversation is null || !conversation.IsVisibleTo(userId, isWorkspaceAdmin))
        {
            return false;
        }

        return await _conversationRepository.DeleteAsync(workspaceId, conversationId, cancellationToken);
    }

    public async Task<ConversationHistoryDto?> GetHistoryAsync(
        GetConversationHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(
            query.WorkspaceId,
            query.ConversationId,
            cancellationToken);

        // RF-18: someone else's conversation answers exactly like a missing one (no existence leak).
        if (conversation is null || !conversation.IsVisibleTo(query.UserId, query.IsWorkspaceAdmin))
        {
            return null;
        }

        var messages = await _conversationRepository.GetMessagesAsync(
            query.WorkspaceId,
            query.ConversationId,
            cancellationToken);

        return new ConversationHistoryDto(
            conversation.Id,
            conversation.WorkspaceId,
            conversation.Title,
            messages
                .Select(x => new ConversationMessageDto(
                    x.Id,
                    x.Role,
                    x.Content,
                    x.ModelName,
                    x.CreatedAtUtc))
                .ToArray());
    }

    public Task<IReadOnlyList<ConversationSummaryDto>> ListAsync(
        ListConversationsQuery query,
        CancellationToken cancellationToken = default)
    {
        return _conversationRepository.ListSummariesAsync(query.WorkspaceId, query.UserId, query.IsWorkspaceAdmin, cancellationToken);
    }
}
