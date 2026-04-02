using Microsoft.Extensions.Logging.Abstractions;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Chat;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Conversations.Dtos;
using RootFlow.Domain.Conversations;
using RootFlow.Domain.Workspaces;

namespace RootFlow.UnitTests.Application;

public sealed class ChatServiceTests
{
    [Fact]
    public async Task AskAsync_HonorsRequestedMaxContextChunks_WhenSearching()
    {
        var searchService = new RecordingKnowledgeSearchService(_ => Array.Empty<KnowledgeSearchMatch>());
        var chatService = CreateChatService(searchService, new RecordingChatCompletionService());

        await chatService.AskAsync(
            new AskQuestionCommand(Guid.NewGuid(), "What is the travel policy?", MaxContextChunks: 3));

        Assert.Equal(3, searchService.CapturedMaxResults);
    }

    [Fact]
    public async Task AskAsync_ReturnsProfessionalFallback_WhenEvidenceIsWeak()
    {
        var weakMatch = new KnowledgeSearchMatch(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "travel-policy.md",
            "Travel Policy",
            "Travel Policy:\nFlights longer than six hours may be booked in premium economy with manager approval.",
            0,
            0.19d,
            0.27d,
            0d,
            0d,
            Array.Empty<string>());

        var searchService = new RecordingKnowledgeSearchService(_ => [weakMatch]);
        var completionService = new RecordingChatCompletionService();
        var chatService = CreateChatService(searchService, completionService);

        var response = await chatService.AskAsync(
            new AskQuestionCommand(Guid.NewGuid(), "What hotline should I use for a security incident?", MaxContextChunks: 3));

        Assert.Contains("limited evidence", response.Answer, StringComparison.OrdinalIgnoreCase);
        Assert.Single(response.Sources);
        Assert.Equal("travel-policy.md", response.Sources[0].DocumentName);
        Assert.Equal(0, completionService.CallCount);
    }

    private static ChatService CreateChatService(
        RecordingKnowledgeSearchService searchService,
        RecordingChatCompletionService completionService)
    {
        return new ChatService(
            new AlwaysExistingWorkspaceRepository(),
            new InMemoryConversationRepository(),
            new StubEmbeddingService(),
            searchService,
            completionService,
            new FixedClock(),
            NullLogger<ChatService>.Instance);
    }

    private sealed class AlwaysExistingWorkspaceRepository : IWorkspaceRepository
    {
        public Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default) => Task.CompletedTask;

        public Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken = default) => Task.FromResult(true);

        public Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default) => Task.FromResult<Workspace?>(null);
    }

    private sealed class InMemoryConversationRepository : IConversationRepository
    {
        private readonly Dictionary<Guid, Conversation> _conversations = [];
        private readonly Dictionary<Guid, List<ConversationMessage>> _messages = [];

        public Task AddAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            _conversations[conversation.Id] = conversation;
            _messages[conversation.Id] = [];
            return Task.CompletedTask;
        }

        public Task UpdateAsync(Conversation conversation, CancellationToken cancellationToken = default)
        {
            _conversations[conversation.Id] = conversation;
            return Task.CompletedTask;
        }

        public Task AddMessageAsync(ConversationMessage message, CancellationToken cancellationToken = default)
        {
            if (!_messages.TryGetValue(message.ConversationId, out var messages))
            {
                messages = [];
                _messages[message.ConversationId] = messages;
            }

            messages.Add(message);
            return Task.CompletedTask;
        }

        public Task<Conversation?> GetByIdAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation)
                && conversation.WorkspaceId == workspaceId)
            {
                return Task.FromResult<Conversation?>(conversation);
            }

            return Task.FromResult<Conversation?>(null);
        }

        public Task<IReadOnlyList<ConversationMessage>> GetMessagesAsync(Guid workspaceId, Guid conversationId, CancellationToken cancellationToken = default)
        {
            if (_conversations.TryGetValue(conversationId, out var conversation)
                && conversation.WorkspaceId == workspaceId
                && _messages.TryGetValue(conversationId, out var messages))
            {
                return Task.FromResult<IReadOnlyList<ConversationMessage>>(messages.ToArray());
            }

            return Task.FromResult<IReadOnlyList<ConversationMessage>>(Array.Empty<ConversationMessage>());
        }

        public Task<IReadOnlyList<ConversationSummaryDto>> ListSummariesAsync(Guid workspaceId, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<ConversationSummaryDto>>(Array.Empty<ConversationSummaryDto>());
        }
    }

    private sealed class StubEmbeddingService : IEmbeddingService
    {
        public Task<float[]> GenerateEmbeddingAsync(string text, CancellationToken cancellationToken = default)
        {
            return Task.FromResult(new[] { 0.42f, 0.58f });
        }

        public Task<IReadOnlyList<float[]>> GenerateEmbeddingsAsync(IReadOnlyList<string> inputs, CancellationToken cancellationToken = default)
        {
            return Task.FromResult<IReadOnlyList<float[]>>(inputs.Select(_ => new[] { 0.42f, 0.58f }).ToArray());
        }
    }

    private sealed class RecordingKnowledgeSearchService : IKnowledgeSearchService
    {
        private readonly Func<string, IReadOnlyList<KnowledgeSearchMatch>> _resultsFactory;

        public RecordingKnowledgeSearchService(Func<string, IReadOnlyList<KnowledgeSearchMatch>> resultsFactory)
        {
            _resultsFactory = resultsFactory;
        }

        public int CapturedMaxResults { get; private set; }

        public Task<IReadOnlyList<KnowledgeSearchMatch>> SearchAsync(
            Guid workspaceId,
            string queryText,
            float[] queryEmbedding,
            int maxResults,
            CancellationToken cancellationToken = default)
        {
            CapturedMaxResults = maxResults;
            return Task.FromResult(_resultsFactory(queryText));
        }
    }

    private sealed class RecordingChatCompletionService : IChatCompletionService
    {
        public int CallCount { get; private set; }

        public Task<ChatCompletionResponse> CompleteAsync(ChatCompletionRequest request, CancellationToken cancellationToken = default)
        {
            CallCount++;
            return Task.FromResult(new ChatCompletionResponse("Structured answer. [1]", "test-model"));
        }
    }

    private sealed class FixedClock : IClock
    {
        public DateTime UtcNow { get; } = new(2026, 4, 2, 12, 0, 0, DateTimeKind.Utc);
    }
}
