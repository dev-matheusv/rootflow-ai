using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Chat.Dtos;
using RootFlow.Domain.Conversations;
using Microsoft.Extensions.Logging;

namespace RootFlow.Application.Chat;

public sealed class ChatService
{
    private const int MaxHistoryMessages = 12;
    private const string InsufficientContextAnswer = "I do not know based on the current knowledge base.";

    private readonly IWorkspaceRepository _workspaceRepository;
    private readonly IConversationRepository _conversationRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IKnowledgeSearchService _knowledgeSearchService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly IClock _clock;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IWorkspaceRepository workspaceRepository,
        IConversationRepository conversationRepository,
        IEmbeddingService embeddingService,
        IKnowledgeSearchService knowledgeSearchService,
        IChatCompletionService chatCompletionService,
        IClock clock,
        ILogger<ChatService> logger)
    {
        _workspaceRepository = workspaceRepository;
        _conversationRepository = conversationRepository;
        _embeddingService = embeddingService;
        _knowledgeSearchService = knowledgeSearchService;
        _chatCompletionService = chatCompletionService;
        _clock = clock;
        _logger = logger;
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
            command.Question,
            queryEmbedding,
            Math.Clamp(command.MaxContextChunks, 1, 10),
            cancellationToken);

        LogSearchResults(conversation.Id, command.Question, searchResults);

        var hasSufficientGrounding = HasSufficientGrounding(searchResults);

        string answer;
        string? modelName = null;

        if (!hasSufficientGrounding)
        {
            answer = InsufficientContextAnswer;
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

        var citedResults = hasSufficientGrounding
            ? searchResults
            : Array.Empty<KnowledgeSearchMatch>();

        return new ChatAnswerDto(
            conversation.Id,
            answer,
            modelName,
            citedResults
                .Select(x => new ChatSourceDto(
                    x.DocumentId,
                    x.ChunkId,
                    x.DocumentName,
                    x.SourceLabel,
                    x.Content,
                    x.Score))
                .ToArray(),
            new ChatRagDebugDto(
                command.Question,
                Math.Min(existingMessages.Count, MaxHistoryMessages),
                searchResults.Count,
                searchResults
                    .Select((x, index) => new ChatRetrievedChunkDebugDto(
                        index + 1,
                        x.ChunkId,
                        x.DocumentName,
                        x.SourceLabel,
                        x.Sequence,
                        x.Score,
                        x.VectorScore,
                        x.KeywordScore,
                        x.MatchedTerms,
                        BuildRankingReason(x)))
                    .ToArray()));
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
        var messages = new List<ChatPromptMessage>(MaxHistoryMessages + 2)
        {
            new(
                MessageRole.System,
                """
                You are RootFlow, a business knowledge assistant.
                Answer only with information supported by the provided context blocks.
                If the context is not enough, reply exactly: "I do not know based on the current knowledge base."
                Keep the answer concise, practical, and factual.
                Cite supporting blocks like [1] after each factual statement or step.
                If the answer is a process, use a short bullet list.
                """)
        };

        foreach (var message in existingMessages.TakeLast(MaxHistoryMessages))
        {
            messages.Add(new ChatPromptMessage(message.Role, message.Content));
        }

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Use the context blocks below to answer the user's question.");
        contextBuilder.AppendLine("Do not invent policies, facts, dates, or numbers.");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Context Blocks:");

        for (var i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            contextBuilder.AppendLine($"[{i + 1}] Document: {result.DocumentName}");
            contextBuilder.AppendLine($"Section: {result.SourceLabel}");
            contextBuilder.AppendLine($"Relevance: combined={result.Score:F3}, vector={result.VectorScore:F3}, keyword={result.KeywordScore:F3}");
            contextBuilder.AppendLine("Content:");
            contextBuilder.AppendLine(result.Content);
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine("Question:");
        contextBuilder.AppendLine(question);
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Write the final answer in clear business English.");
        contextBuilder.AppendLine("Add citations like [1] directly after supported statements.");

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

    private void LogSearchResults(
        Guid conversationId,
        string question,
        IReadOnlyList<KnowledgeSearchMatch> searchResults)
    {
        var rankedResults = searchResults
            .Select((result, index) => new
            {
                Rank = index + 1,
                result.DocumentName,
                result.SourceLabel,
                result.Sequence,
                Score = Math.Round(result.Score, 4),
                VectorScore = Math.Round(result.VectorScore, 4),
                KeywordScore = Math.Round(result.KeywordScore, 4),
                result.MatchedTerms
            })
            .ToArray();

        _logger.LogInformation(
            "RAG retrieval for conversation {ConversationId} and question {Question} returned {@Results}",
            conversationId,
            question,
            rankedResults);
    }

    private static string BuildRankingReason(KnowledgeSearchMatch result)
    {
        if (result.MatchedTerms.Count == 0)
        {
            return $"Vector score {result.VectorScore:F3} with no keyword matches.";
        }

        return $"Matched terms: {string.Join(", ", result.MatchedTerms)}. Vector score {result.VectorScore:F3}, keyword score {result.KeywordScore:F3}, combined score {result.Score:F3}.";
    }

    private static bool HasSufficientGrounding(IReadOnlyList<KnowledgeSearchMatch> searchResults)
    {
        if (searchResults.Count == 0)
        {
            return false;
        }

        var topResult = searchResults[0];
        if (topResult.KeywordScore >= 0.2)
        {
            return true;
        }

        return topResult.Score >= 0.35 && topResult.VectorScore >= 0.4;
    }
}
