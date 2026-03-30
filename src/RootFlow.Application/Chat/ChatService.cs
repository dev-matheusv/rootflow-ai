using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Chat.Dtos;
using RootFlow.Domain.Conversations;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace RootFlow.Application.Chat;

public sealed class ChatService
{
    private const int MaxHistoryMessages = 12;
    private const string InsufficientContextAnswer = "I do not know based on the current knowledge base.";
    private static readonly Regex ExcessWhitespaceRegex = new(@"\n{3,}", RegexOptions.Compiled);

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
            answer = CleanAnswer(completion.Content);
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
                Answer the user's exact question using only the provided context blocks.
                Treat earlier assistant messages as non-authoritative drafts. Override them whenever the retrieved context says otherwise.
                If the context is insufficient or does not answer the exact question, reply exactly: "I do not know based on the current knowledge base."
                Write in clear, professional plain text.
                Start with a direct answer. Add a short bullet list only when it improves clarity.
                Keep the response concise but complete.
                Do not dump raw context or mention retrieval scores.
                Cite supported statements with [1], [2], etc. Use only the provided block numbers.
                Do not cite a block unless it directly supports the statement.
                """)
        };

        foreach (var message in existingMessages.TakeLast(MaxHistoryMessages))
        {
            messages.Add(new ChatPromptMessage(message.Role, message.Content));
        }

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine("Use the context blocks below to answer the user's question.");
        contextBuilder.AppendLine("Most relevant blocks are listed first.");
        contextBuilder.AppendLine("Do not invent policies, facts, dates, numbers, or steps.");
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Context Blocks:");

        for (var i = 0; i < searchResults.Count; i++)
        {
            var result = searchResults[i];
            contextBuilder.AppendLine($"[{i + 1}] Document: {result.DocumentName}");
            contextBuilder.AppendLine($"Section: {result.SourceLabel}");
            contextBuilder.AppendLine("Content:");
            contextBuilder.AppendLine(result.Content);
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine("Question:");
        contextBuilder.AppendLine(question);
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Write the final answer in clear business English.");
        contextBuilder.AppendLine("Answer the exact question, not a nearby topic.");
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
        var runnerUp = searchResults.Count > 1 ? searchResults[1] : null;
        var scoreMargin = runnerUp is null
            ? topResult.Score
            : topResult.Score - runnerUp.Score;

        if (topResult.KeywordScore >= 0.45)
        {
            return true;
        }

        if (topResult.KeywordScore >= 0.25 && topResult.VectorScore >= 0.45)
        {
            return true;
        }

        return topResult.Score >= 0.55
            && topResult.VectorScore >= 0.72
            && scoreMargin >= 0.05;
    }

    private static string CleanAnswer(string answer)
    {
        var normalized = answer
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        normalized = ExcessWhitespaceRegex.Replace(normalized, "\n\n");
        return normalized;
    }
}
