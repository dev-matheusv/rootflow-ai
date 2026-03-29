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

    public async Task<ConversationHistoryDto?> GetHistoryAsync(
        GetConversationHistoryQuery query,
        CancellationToken cancellationToken = default)
    {
        var conversation = await _conversationRepository.GetByIdAsync(
            query.WorkspaceId,
            query.ConversationId,
            cancellationToken);

        if (conversation is null)
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
        return _conversationRepository.ListSummariesAsync(query.WorkspaceId, cancellationToken);
    }
}
