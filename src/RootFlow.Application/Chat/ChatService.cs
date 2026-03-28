using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Chat.Dtos;
using RootFlow.Domain.Conversations;

namespace RootFlow.Application.Chat;

public sealed class ChatService
{
    private const int MaxHistoryMessages = 12;

    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IKnowledgeSearchService _knowledgeSearchService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IClock _clock;

    public ChatService(
        IWorkspaceRepository workspaceRepository,
        IConversationRepository conversationRepository,
        IEmbeddingService embeddingService,
        IKnowledgeSearchService knowledgeSearchService,
        IChatCompletionService chatCompletionService,
        IClock clock)
    {
        _workspaceRepository = workspaceRepository;
        _conversationRepository = conversationRepository;
        _embeddingService = embeddingService;
        _knowledgeSearchService = knowledgeSearchService;
        _chatCompletionService = chatCompletionService;
        _clock = clock;
    }

    public async Task<ChatAnswerDto> AskAsync(
        AskQuestionCommand command,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(command.Question))
        {
            throw new ArgumentException("Question is required.", nameof(command));
        }

        var workspaceExists = await _workspaceRepository.ExistsAsync(command.WorkspaceId, cancellationToken);
        if (!workspaceExists)
        {
            throw new InvalidOperationException("Workspace was not found.");
        }

        var conversation = await GetOrCreateConversationAsync(command, cancellationToken);

        var existingMessages = await _conversationRepository.GetMessagesAsync(
            command.WorkspaceId,
            conversation.Id,
            cancellationToken);

        var userMessage = new ConversationMessage(
            Guid.NewGuid(),
            conversation.Id,
            MessageRole.User,
            command.Question,
            _clock.UtcNow);

        await _conversationRepository.AddMessageAsync(userMessage, cancellationToken);

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(command.Question, cancellationToken);
        var searchResults = await _knowledgeSearchService.SearchAsync(
            command.WorkspaceId,
            queryEmbedding,
            Math.Clamp(command.MaxContextChunks, 1, 10),
            cancellationToken);

        string answer;
        string? modelName = null;

        if (searchResults.Count == 0)
        {
            answer = "I could not find relevant information in the current knowledge base.";
        }
        else
        {
            var prompt = BuildPrompt(existingMessages, command.Question, searchResults);
            var completion = await _chatCompletionService.CompleteAsync(prompt, cancellationToken);
            answer = completion.Content;
            modelName = completion.ModelName;
        }

        var assistantMessage = new ConversationMessage(
            Guid.NewGuid(),
            conversation.Id,
            MessageRole.Assistant,
            answer,
            _clock.UtcNow,
            modelName);

        await _conversationRepository.AddMessageAsync(assistantMessage, cancellationToken);

        conversation.Touch(_clock.UtcNow);
        await _conversationRepository.UpdateAsync(conversation, cancellationToken);

        return new ChatAnswerDto(
            conversation.Id,
            answer,
            modelName,
            searchResults
                .Select(x => new ChatSourceDto(
                    x.DocumentId,
                    x.ChunkId,
                    x.DocumentName,
                    x.Content,
                    x.Score))
                .ToArray());
    }

    private async Task<Conversation> GetOrCreateConversationAsync(
        AskQuestionCommand command,
        CancellationToken cancellationToken)
    {
        if (command.ConversationId.HasValue)
        {
            var existingConversation = await _conversationRepository.GetByIdAsync(
                command.WorkspaceId,
                command.ConversationId.Value,
                cancellationToken);

            if (existingConversation is null)
            {
                throw new InvalidOperationException("Conversation was not found.");
            }

            return existingConversation;
        }

        var conversation = new Conversation(
            Guid.NewGuid(),
            command.WorkspaceId,
            CreateConversationTitle(command.Question),
            _clock.UtcNow);

        await _conversationRepository.AddAsync(conversation, cancellationToken);
        return conversation;
    }

    private ChatCompletionRequest BuildPrompt(
        IReadOnlyList<ConversationMessage> existingMessages,
        string question,
        IReadOnlyList<KnowledgeSearchMatch> searchResults)
    {
        var messages = new List<ChatPromptMessage>(MaxHistoryMessages + 1);

        foreach (var message in existingMessages.TakeLast(MaxHistoryMessages))
        {
            messages.Add(new ChatPromptMessage(message.Role, message.Content));
        }

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Use the context below to answer the user's question.");
        contextBuilder.AppendLine("If the context is not enough, say that you do not know based on the current knowledge base.");
        contextBuilder.AppendLine("Do not invent policies, facts, or numbers.");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Context:");

        for (var i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            contextBuilder.AppendLine($"[{i + 1}] {result.DocumentName}");
            contextBuilder.AppendLine(result.Content);
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine("Question:");
        contextBuilder.AppendLine(question);
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Answer in clear business English and cite the source blocks like [1] when relevant.");

        messages.Add(new ChatPromptMessage(MessageRole.User, contextBuilder.ToString()));

        return new ChatCompletionRequest(messages);
    }

    private static string CreateConversationTitle(string question)
    {
        var title = question.Trim();
        if (title.Length <= 80)
        {
            return title;
        }

        return $"{title[..77]}...";
    }
}
