using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Billing;
using RootFlow.Application.Billing.Commands;
using RootFlow.Application.Chat.Commands;
using RootFlow.Application.Chat.Dtos;
using RootFlow.Domain.Conversations;
using Microsoft.Extensions.Logging;
using System.Text.RegularExpressions;

namespace RootFlow.Application.Chat;

public sealed class ChatService
{
    private const int MaxHistoryMessages = 12;
    private const int MaxRecentUserMessages = 3;
    private static readonly Regex ExcessWhitespaceRegex = new(@"\n{3,}", RegexOptions.Compiled);
    private static readonly Regex CitationRegex = new(@"\[(\d+)\]", RegexOptions.Compiled);

    private readonly IConversationRepository _conversationRepository;
    private readonly IEmbeddingService _embeddingService;
    private readonly IKnowledgeSearchService _knowledgeSearchService;
    private readonly IChatCompletionService _chatCompletionService;
    private readonly WorkspaceBillingService _workspaceBillingService;
    private readonly IClock _clock;
    private readonly ILogger<ChatService> _logger;

    public ChatService(
        IConversationRepository conversationRepository,
        IEmbeddingService embeddingService,
        IKnowledgeSearchService knowledgeSearchService,
        IChatCompletionService chatCompletionService,
        WorkspaceBillingService workspaceBillingService,
        IClock clock,
        ILogger<ChatService> logger)
    {
        _conversationRepository = conversationRepository;
        _embeddingService = embeddingService;
        _knowledgeSearchService = knowledgeSearchService;
        _chatCompletionService = chatCompletionService;
        _workspaceBillingService = workspaceBillingService;
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

        await _workspaceBillingService.EnsureAssistantUsageAllowedAsync(command.WorkspaceId, cancellationToken);

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

        var turnContext = ResolveTurnContext(existingMessages, command.Question);
        var retrievalQuery = SemanticQueryExpander.Expand(turnContext.RetrievalInput);
        var retrievalWindow = Math.Clamp(command.MaxContextChunks, 1, 8);

        var queryEmbedding = await _embeddingService.GenerateEmbeddingAsync(
            retrievalQuery.RetrievalText,
            cancellationToken);
        var searchResults = await _knowledgeSearchService.SearchAsync(
            command.WorkspaceId,
            turnContext.RetrievalInput,
            queryEmbedding,
            retrievalWindow,
            cancellationToken);

        var retrievalAssessment = AssessRetrieval(searchResults);
        LogSearchResults(
            conversation.Id,
            command.Question,
            retrievalQuery.RetrievalText,
            retrievalAssessment.Strength,
            searchResults);

        var hasRelevantContext = retrievalAssessment.Strength is not RetrievalEvidenceStrength.None;

        string answer;
        string? modelName = null;

        if (retrievalAssessment.Strength == RetrievalEvidenceStrength.None)
        {
            answer = ChatLanguageDetector.GetNoContextAnswer(turnContext.Language);
        }
        else if (retrievalAssessment.Strength == RetrievalEvidenceStrength.Weak)
        {
            answer = BuildWeakEvidenceAnswer(turnContext.Language, searchResults[0]);
        }
        else
        {
            var prompt = BuildPrompt(turnContext, searchResults, retrievalAssessment.Strength);
            var completion = await _chatCompletionService.CompleteAsync(prompt, cancellationToken);
            answer = CleanAnswer(completion.Content);
            modelName = completion.ModelName;

            try
            {
                var usage = completion.Usage;
                var usageEvent = await _workspaceBillingService.RegisterUsageAsync(
                    new RegisterWorkspaceUsageCommand(
                        command.WorkspaceId,
                        command.UserId,
                        conversation.Id,
                        completion.Provider,
                        completion.ModelName ?? "unknown",
                        usage?.PromptTokens ?? 0,
                        usage?.CompletionTokens ?? 0,
                        usage?.TotalTokens ?? 0),
                    cancellationToken);

                _logger.LogInformation(
                    "Recorded assistant usage for workspace {WorkspaceId}, conversation {ConversationId}, usage event {UsageEventId}, model {Model}, provider {Provider}, total tokens {TotalTokens}, credits charged {CreditsCharged}.",
                    command.WorkspaceId,
                    conversation.Id,
                    usageEvent.Id,
                    usageEvent.Model,
                    usageEvent.Provider,
                    usageEvent.TotalTokens,
                    usageEvent.CreditsCharged);
            }
            catch (InsufficientWorkspaceCreditsException exception)
            {
                _logger.LogWarning(
                    exception,
                    "Assistant usage for workspace {WorkspaceId} and conversation {ConversationId} could not be finalized because the remaining credit balance was insufficient after the provider response.",
                    command.WorkspaceId,
                    conversation.Id);

                throw;
            }
            catch (Exception exception)
            {
                _logger.LogError(
                    exception,
                    "Failed to register assistant usage for workspace {WorkspaceId}, conversation {ConversationId}, provider {Provider}, and model {Model}.",
                    command.WorkspaceId,
                    conversation.Id,
                    completion.Provider,
                    completion.ModelName ?? "unknown");

                throw;
            }
        }

        var usedChunkRanks = ExtractCitedChunkRanks(answer, searchResults.Count);
        LogAnswerGrounding(conversation.Id, command.Question, answer, searchResults, usedChunkRanks);

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

        var citedResults = hasRelevantContext
            ? SelectSourceResults(searchResults, usedChunkRanks)
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
                retrievalQuery.RetrievalText,
                Math.Min(existingMessages.Count, MaxHistoryMessages),
                searchResults.Count,
                usedChunkRanks,
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
                        x.PhraseScore,
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
        ResolvedTurnContext turnContext,
        IReadOnlyList<KnowledgeSearchMatch> searchResults,
        RetrievalEvidenceStrength evidenceStrength)
    {
        var noContextAnswer = ChatLanguageDetector.GetNoContextAnswer(turnContext.Language);
        var languageLabel = ChatLanguageDetector.GetPromptLanguageLabel(turnContext.Language);
        var evidenceStrengthLabel = evidenceStrength switch
        {
            RetrievalEvidenceStrength.Strong => "strong",
            RetrievalEvidenceStrength.Moderate => "moderate",
            RetrievalEvidenceStrength.Weak => "weak",
            _ => "none"
        };

        var messages = new List<ChatPromptMessage>(2)
        {
            new(
                MessageRole.System,
                $$"""
                You are RootFlow, a business knowledge assistant.
                The response language MUST exactly match {{languageLabel}}.
                Respond entirely in {{languageLabel}}. Never mix languages in the narrative, labels, or connective text.
                If any sentence, label, bullet, or connective phrase appears in another language, rewrite the full response entirely in {{languageLabel}} before returning it.
                Answer the resolved user intent using only the provided context blocks.
                If the context block list is empty, reply exactly: "{{noContextAnswer}}"
                If at least one context block is provided, answer with the best grounded response you can. Do not fall back to uncertainty when usable evidence exists.
                Start with a direct answer line.
                If evidence is partial, say that clearly and stay narrow.
                Distinguish direct document findings from best inference when you need to infer.
                Only call something a finding from the documents when it is directly supported by the provided blocks.
                When the answer includes multiple facts, categories, steps, meals, skills, roles, dates, employers, education items, or technologies, use a structured format with blank lines between sections.
                Use top-level bullets for sections and indented "-" bullets for details.
                Never return a dense paragraph when structured bullets would be clearer.
                Keep the answer concise, natural, professional, and easy to scan.
                Synthesize across blocks when needed, but stay grounded in the provided evidence.
                Do not dump raw context or mention retrieval scores.
                Cite only the block numbers you actually used, such as [1] or [2].
                Do not cite a block unless it directly supports the statement.
                For resume or curriculum questions, prefer clean sections such as Experience, Education, Skills, Qualifications, and Current Company when they are relevant.
                For diet or training questions, prefer sections such as Recommendation, Schedule, Breakdown, Notes, and Next Step when they are relevant.
                Format example:
                Direct answer sentence. [1]

                - Section:
                  - detail [1]
                  - detail [2]
                """)
        };

        var contextBuilder = new System.Text.StringBuilder();
        contextBuilder.AppendLine($"Answer language: {languageLabel}");
        contextBuilder.AppendLine($"Evidence strength: {evidenceStrengthLabel}");
        contextBuilder.AppendLine("Use the context blocks below to answer the user's resolved intent.");
        contextBuilder.AppendLine("Most relevant blocks are listed first.");
        contextBuilder.AppendLine("Some blocks may be only partially relevant. Use the strongest evidence and ignore weak matches.");
        contextBuilder.AppendLine("If any block answers part of the question, answer from it instead of falling back to uncertainty.");
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

        if (!string.IsNullOrWhiteSpace(turnContext.RecentUserContext))
        {
            contextBuilder.AppendLine("Recent user-only conversation context:");
            contextBuilder.AppendLine(turnContext.RecentUserContext);
            contextBuilder.AppendLine();
        }

        contextBuilder.AppendLine("Resolved intent:");
        contextBuilder.AppendLine(turnContext.ResolvedQuestion);
        contextBuilder.AppendLine();
        contextBuilder.AppendLine("Current user message:");
        contextBuilder.AppendLine(turnContext.OriginalQuestion);
        contextBuilder.AppendLine();
        contextBuilder.AppendLine($"Write the final answer entirely in {languageLabel}.");
        contextBuilder.AppendLine("Answer the exact user intent, not a nearby topic.");
        contextBuilder.AppendLine("Prefer the most specific block when several blocks discuss the same subject.");
        contextBuilder.AppendLine("When the answer is list-like, use clean sections and nested bullets instead of a dense paragraph.");
        contextBuilder.AppendLine("For short factual answers, keep it to one or two concise lines.");
        contextBuilder.AppendLine("If you are inferring something from the documents, label it as a best inference instead of a direct finding.");
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

    private static string BuildRetrievalInput(
        IReadOnlyList<ConversationMessage> existingMessages,
        string question)
    {
        if (!IsFollowUpQuestion(question))
        {
            return question;
        }

        var recentUserMessages = existingMessages
            .Where(static message => message.Role == MessageRole.User)
            .TakeLast(MaxRecentUserMessages)
            .Select(static message => message.Content.Trim())
            .Where(static content => !string.IsNullOrWhiteSpace(content))
            .ToArray();

        if (recentUserMessages.Length == 0)
        {
            return question;
        }

        return $$"""
                 Prior user context:
                 {{string.Join(Environment.NewLine, recentUserMessages)}}

                 Follow-up question:
                 {{question}}
                 """;
    }

    private static string? BuildRecentUserContext(
        IReadOnlyList<ConversationMessage> existingMessages,
        string question)
    {
        if (!IsFollowUpQuestion(question))
        {
            return null;
        }

        var recentUserMessages = existingMessages
            .Where(static message => message.Role == MessageRole.User)
            .TakeLast(MaxRecentUserMessages)
            .Select(static message => message.Content.Trim())
            .Where(static content => !string.IsNullOrWhiteSpace(content))
            .ToArray();

        if (recentUserMessages.Length == 0)
        {
            return null;
        }

        return string.Join(Environment.NewLine, recentUserMessages.Select(static content => $"- {content}"));
    }

    private static bool IsFollowUpQuestion(string question)
    {
        var normalized = question.Trim().ToLowerInvariant();
        if (normalized.Length == 0)
        {
            return false;
        }

        return normalized.StartsWith("and ", StringComparison.Ordinal)
            || normalized.StartsWith("also ", StringComparison.Ordinal)
            || normalized.StartsWith("what about", StringComparison.Ordinal)
            || normalized.StartsWith("how about", StringComparison.Ordinal)
            || normalized.StartsWith("e ", StringComparison.Ordinal)
            || normalized.StartsWith("tambem ", StringComparison.Ordinal)
            || normalized.StartsWith("também ", StringComparison.Ordinal)
            || normalized.Contains("current role", StringComparison.Ordinal)
            || normalized.Contains("current job", StringComparison.Ordinal)
            || normalized.Contains("what about", StringComparison.Ordinal)
            || normalized.Contains("his ", StringComparison.Ordinal)
            || normalized.Contains("her ", StringComparison.Ordinal)
            || normalized.Contains("their ", StringComparison.Ordinal)
            || normalized.Contains("that ", StringComparison.Ordinal)
            || normalized.Contains("this ", StringComparison.Ordinal)
            || normalized.Contains("those ", StringComparison.Ordinal)
            || normalized.Contains("these ", StringComparison.Ordinal);
    }

    private static ResolvedTurnContext ResolveTurnContext(
        IReadOnlyList<ConversationMessage> existingMessages,
        string question)
    {
        var priorUserMessages = existingMessages
            .Where(static message => message.Role == MessageRole.User)
            .Select(static message => message.Content.Trim())
            .Where(static content => !string.IsNullOrWhiteSpace(content))
            .TakeLast(MaxRecentUserMessages)
            .ToArray();

        var languageInputs = priorUserMessages
            .Concat([question])
            .ToArray();
        var language = ChatLanguageDetector.Detect(languageInputs);
        if (!ShouldResolveFollowUp(question, priorUserMessages))
        {
            return new ResolvedTurnContext(
                question,
                question,
                question,
                language,
                null);
        }

        var anchorQuestion = SelectAnchorUserMessage(priorUserMessages);
        if (string.IsNullOrWhiteSpace(anchorQuestion))
        {
            return new ResolvedTurnContext(
                question,
                question,
                question,
                language,
                null);
        }

        var resolvedQuestion = BuildResolvedQuestion(anchorQuestion, question, language);
        var recentUserContext = string.Join(
            Environment.NewLine,
            priorUserMessages.Select(static content => $"- {content}"));

        return new ResolvedTurnContext(
            question,
            resolvedQuestion,
            resolvedQuestion,
            language,
            recentUserContext);
    }

    private static bool ShouldResolveFollowUp(string question, IReadOnlyList<string> priorUserMessages)
    {
        if (priorUserMessages.Count == 0)
        {
            return false;
        }

        var normalizedQuestion = SemanticQueryExpander.NormalizeText(question);
        return StartsWithFollowUpCue(normalizedQuestion)
            || IsContextStatement(question, normalizedQuestion)
            || IsShortReferentialFollowUp(normalizedQuestion);
    }

    private static string? SelectAnchorUserMessage(IReadOnlyList<string> priorUserMessages)
    {
        for (var index = priorUserMessages.Count - 1; index >= 0; index--)
        {
            var candidate = priorUserMessages[index];
            var normalizedCandidate = SemanticQueryExpander.NormalizeText(candidate);
            if (!StartsWithFollowUpCue(normalizedCandidate) && !IsContextStatement(candidate, normalizedCandidate))
            {
                return candidate;
            }
        }

        return priorUserMessages.LastOrDefault();
    }

    private static string BuildResolvedQuestion(
        string anchorQuestion,
        string followUpQuestion,
        ChatLanguage language)
    {
        var normalizedFollowUp = SemanticQueryExpander.NormalizeText(followUpQuestion);
        var anchor = TrimTrailingSentencePunctuation(anchorQuestion);
        var followUp = TrimTrailingSentencePunctuation(followUpQuestion.Trim());

        if (IsContextStatement(followUpQuestion, normalizedFollowUp))
        {
            var loweredContext = LowercaseFirst(followUp);
            return language == ChatLanguage.Portuguese
                ? $"{anchor} considerando que {loweredContext}."
                : $"{anchor} considering that {loweredContext}.";
        }

        var focus = StripFollowUpLead(followUp);
        if (StartsWithFollowUpCue(normalizedFollowUp))
        {
            var loweredFocus = LowercaseFirst(string.IsNullOrWhiteSpace(focus) ? followUp : focus);
            return language == ChatLanguage.Portuguese
                ? $"{anchor}. Agora responda especificamente sobre {loweredFocus}."
                : $"{anchor}. Now answer specifically about {loweredFocus}.";
        }

        return language == ChatLanguage.Portuguese
            ? $"{anchor}. Contexto adicional: {followUp}."
            : $"{anchor}. Additional context: {followUp}.";
    }

    private static bool StartsWithFollowUpCue(string normalizedQuestion)
    {
        return normalizedQuestion.StartsWith("and ", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("also ", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("what about", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("how about", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("e ", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("tambem ", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("e o ", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("e a ", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("e os ", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("e as ", StringComparison.Ordinal);
    }

    private static bool IsShortReferentialFollowUp(string normalizedQuestion)
    {
        var tokens = SemanticQueryExpander.Tokenize(normalizedQuestion);
        if (tokens.Count == 0 || tokens.Count > 8)
        {
            return false;
        }

        return normalizedQuestion.Contains("rest", StringComparison.Ordinal)
            || normalizedQuestion.Contains("resto", StringComparison.Ordinal)
            || normalizedQuestion.Contains("my case", StringComparison.Ordinal)
            || normalizedQuestion.Contains("meu caso", StringComparison.Ordinal)
            || normalizedQuestion.Contains("minha formacao", StringComparison.Ordinal)
            || normalizedQuestion.Contains("my education", StringComparison.Ordinal)
            || normalizedQuestion.Contains("my experience", StringComparison.Ordinal)
            || normalizedQuestion.Contains("minha experiencia", StringComparison.Ordinal)
            || normalizedQuestion.Contains("current company", StringComparison.Ordinal)
            || normalizedQuestion.Contains("empresa atual", StringComparison.Ordinal);
    }

    private static bool IsContextStatement(string originalQuestion, string normalizedQuestion)
    {
        if (originalQuestion.Contains('?', StringComparison.Ordinal))
        {
            return false;
        }

        return normalizedQuestion.StartsWith("today", StringComparison.Ordinal)
            || normalizedQuestion.StartsWith("hoje", StringComparison.Ordinal)
            || normalizedQuestion.Contains("monday", StringComparison.Ordinal)
            || normalizedQuestion.Contains("segunda", StringComparison.Ordinal)
            || normalizedQuestion.Contains("tuesday", StringComparison.Ordinal)
            || normalizedQuestion.Contains("terca", StringComparison.Ordinal)
            || normalizedQuestion.Contains("wednesday", StringComparison.Ordinal)
            || normalizedQuestion.Contains("quarta", StringComparison.Ordinal)
            || normalizedQuestion.Contains("thursday", StringComparison.Ordinal)
            || normalizedQuestion.Contains("quinta", StringComparison.Ordinal)
            || normalizedQuestion.Contains("friday", StringComparison.Ordinal)
            || normalizedQuestion.Contains("sexta", StringComparison.Ordinal)
            || normalizedQuestion.Contains("in my case", StringComparison.Ordinal)
            || normalizedQuestion.Contains("no meu caso", StringComparison.Ordinal);
    }

    private static string StripFollowUpLead(string question)
    {
        var trimmed = question.Trim();
        var normalized = SemanticQueryExpander.NormalizeText(trimmed);

        var prefixes = new Dictionary<string, int>(StringComparer.Ordinal)
        {
            ["and what about "] = "And what about ".Length,
            ["what about "] = "What about ".Length,
            ["how about "] = "How about ".Length,
            ["and "] = "And ".Length,
            ["also "] = "Also ".Length,
            ["e o "] = "E o ".Length,
            ["e a "] = "E a ".Length,
            ["e os "] = "E os ".Length,
            ["e as "] = "E as ".Length,
            ["e "] = "E ".Length,
            ["tambem "] = "Tambem ".Length
        };

        foreach (var prefix in prefixes)
        {
            if (normalized.StartsWith(prefix.Key, StringComparison.Ordinal) && trimmed.Length > prefix.Value)
            {
                return trimmed[prefix.Value..].Trim();
            }
        }

        return trimmed;
    }

    private static string TrimTrailingSentencePunctuation(string value)
    {
        return value.Trim().TrimEnd('.', '?', '!', ';', ':');
    }

    private static string LowercaseFirst(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return value;
        }

        return char.ToLowerInvariant(value[0]) + value[1..];
    }

    private static RetrievalAssessment AssessRetrieval(IReadOnlyList<KnowledgeSearchMatch> searchResults)
    {
        if (searchResults.Count == 0)
        {
            return new RetrievalAssessment(RetrievalEvidenceStrength.None);
        }

        var top = searchResults[0];
        var lexicalSignal = top.KeywordScore >= 0.10d
            || top.PhraseScore >= 0.10d
            || top.MatchedTerms.Count >= 2;
        var usableLexicalSignal = top.KeywordScore >= 0.02d
            || top.PhraseScore > 0d
            || top.MatchedTerms.Count > 0;
        var topGroupCount = searchResults.Count(result =>
            result.Score >= top.Score * 0.84d
            || result.KeywordScore >= 0.10d
            || result.PhraseScore >= 0.10d);

        if (top.Score < 0.16d && top.VectorScore < 0.24d && !usableLexicalSignal)
        {
            return new RetrievalAssessment(RetrievalEvidenceStrength.None);
        }

        if (top.Score >= 0.42d && (lexicalSignal || top.VectorScore >= 0.50d))
        {
            return new RetrievalAssessment(RetrievalEvidenceStrength.Strong);
        }

        if (searchResults.Count == 1
            && top.Score < 0.21d
            && top.VectorScore < 0.28d
            && !usableLexicalSignal
            && topGroupCount == 1)
        {
            return new RetrievalAssessment(RetrievalEvidenceStrength.Weak);
        }

        return new RetrievalAssessment(RetrievalEvidenceStrength.Moderate);
    }

    private static string BuildWeakEvidenceAnswer(ChatLanguage language, KnowledgeSearchMatch closestMatch)
    {
        var location = BuildEvidenceLocation(closestMatch);

        return language == ChatLanguage.Portuguese
            ? $"Encontrei apenas evidência limitada relacionada a essa pergunta, então não posso responder com confiança com base nos documentos atuais. O trecho mais próximo está em {location}. [1]"
            : $"I found only limited evidence related to that question, so I cannot answer confidently from the current documents. The closest relevant material is in {location}. [1]";
    }

    private static string BuildEvidenceLocation(KnowledgeSearchMatch match)
    {
        if (string.IsNullOrWhiteSpace(match.SourceLabel))
        {
            return $"\"{match.DocumentName}\"";
        }

        return $"\"{match.DocumentName}\" ({match.SourceLabel})";
    }

    private void LogSearchResults(
        Guid conversationId,
        string question,
        string retrievalQuery,
        RetrievalEvidenceStrength evidenceStrength,
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
                PhraseScore = Math.Round(result.PhraseScore, 4),
                result.MatchedTerms
            })
            .ToArray();

        _logger.LogInformation(
            "RAG retrieval for conversation {ConversationId} and question {Question} using retrieval query {RetrievalQuery} assessed as {EvidenceStrength} returned {@Results}",
            conversationId,
            question,
            retrievalQuery,
            evidenceStrength,
            rankedResults);
    }

    private static string BuildRankingReason(KnowledgeSearchMatch result)
    {
        if (result.MatchedTerms.Count == 0)
        {
            return $"Vector score {result.VectorScore:F3}, phrase score {result.PhraseScore:F3}, with no keyword matches.";
        }

        return $"Matched terms: {string.Join(", ", result.MatchedTerms)}. Vector score {result.VectorScore:F3}, keyword score {result.KeywordScore:F3}, phrase score {result.PhraseScore:F3}, combined score {result.Score:F3}.";
    }

    private void LogAnswerGrounding(
        Guid conversationId,
        string question,
        string answer,
        IReadOnlyList<KnowledgeSearchMatch> searchResults,
        IReadOnlyList<int> usedChunkRanks)
    {
        var usedChunks = usedChunkRanks
            .Where(rank => rank >= 1 && rank <= searchResults.Count)
            .Select(rank => new
            {
                CitationRank = rank,
                searchResults[rank - 1].DocumentName,
                searchResults[rank - 1].SourceLabel,
                searchResults[rank - 1].Sequence
            })
            .ToArray();

        _logger.LogInformation(
            "RAG answer for conversation {ConversationId} and question {Question} used chunks {UsedChunks} and produced answer {Answer}",
            conversationId,
            question,
            usedChunks,
            answer);
    }

    private static string CleanAnswer(string answer)
    {
        var normalized = answer
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (normalized.StartsWith("Based on the knowledge base,", StringComparison.OrdinalIgnoreCase))
        {
            normalized = normalized["Based on the knowledge base,".Length..].TrimStart();
        }

        normalized = NormalizeStructuredLayout(normalized);
        normalized = ExcessWhitespaceRegex.Replace(normalized, "\n\n");
        return normalized;
    }

    private static IReadOnlyList<int> ExtractCitedChunkRanks(string answer, int availableChunkCount)
    {
        if (string.IsNullOrWhiteSpace(answer) || availableChunkCount == 0)
        {
            return Array.Empty<int>();
        }

        return CitationRegex.Matches(answer)
            .Select(static match => int.TryParse(match.Groups[1].Value, out var rank) ? rank : 0)
            .Where(rank => rank >= 1 && rank <= availableChunkCount)
            .Distinct()
            .ToArray();
    }

    private static IReadOnlyList<KnowledgeSearchMatch> SelectSourceResults(
        IReadOnlyList<KnowledgeSearchMatch> searchResults,
        IReadOnlyList<int> usedChunkRanks)
    {
        if (searchResults.Count == 0)
        {
            return Array.Empty<KnowledgeSearchMatch>();
        }

        if (usedChunkRanks.Count == 0)
        {
            return [searchResults[0]];
        }

        return usedChunkRanks
            .Where(rank => rank >= 1 && rank <= searchResults.Count)
            .Select(rank => searchResults[rank - 1])
            .Distinct()
            .ToArray();
    }

    private static string NormalizeStructuredLayout(string answer)
    {
        var lines = answer.Split('\n');
        var builder = new System.Text.StringBuilder(answer.Length + 32);
        var previousWasBlank = true;

        foreach (var rawLine in lines)
        {
            var line = rawLine.TrimEnd();
            var isBlank = string.IsNullOrWhiteSpace(line);
            var isTopLevelBullet = line.StartsWith("- ", StringComparison.Ordinal) || line.StartsWith("• ", StringComparison.Ordinal);

            if (isBlank)
            {
                if (!previousWasBlank)
                {
                    builder.AppendLine();
                }

                previousWasBlank = true;
                continue;
            }

            if (isTopLevelBullet && builder.Length > 0 && !previousWasBlank)
            {
                builder.AppendLine();
            }

            builder.AppendLine(line);
            previousWasBlank = false;
        }

        return builder.ToString().Trim();
    }

    private enum RetrievalEvidenceStrength
    {
        None,
        Weak,
        Moderate,
        Strong
    }

    private sealed record RetrievalAssessment(RetrievalEvidenceStrength Strength);

    private sealed record ResolvedTurnContext(
        string OriginalQuestion,
        string ResolvedQuestion,
        string RetrievalInput,
        ChatLanguage Language,
        string? RecentUserContext);
}
