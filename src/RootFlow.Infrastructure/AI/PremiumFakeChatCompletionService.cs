using System.Text;
using System.Text.RegularExpressions;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Chat;

namespace RootFlow.Infrastructure.AI;

public sealed class PremiumFakeChatCompletionService : IChatCompletionService
{
    private static readonly Regex BlockRegex = new(
        @"\[(\d+)\]\s+[^\n]+\n(.*?)(?=\n\[\d+\]\s+[^\n]+\n|\z)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    public Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = request.Messages.LastOrDefault()?.Content ?? string.Empty;
        var resolvedIntent = ExtractSection(prompt, "Resolved intent:", "Current user message:");
        var currentMessage = ExtractSection(prompt, "Current user message:", "Write the final answer");
        var question = string.IsNullOrWhiteSpace(resolvedIntent) ? currentMessage : resolvedIntent;
        var context = ExtractSection(prompt, "Context Blocks:", "Resolved intent:");
        var blocks = ParseBlocks(context);
        var language = ChatLanguageDetector.Detect(currentMessage, question);
        var expandedQuestion = SemanticQueryExpander.Expand(question);
        var answerIntent = SemanticQueryExpander.Expand(
            string.IsNullOrWhiteSpace(currentMessage) ? question : currentMessage);

        if (blocks.Count == 0)
        {
            var noContextAnswer = ChatLanguageDetector.GetNoContextAnswer(language);
            return Task.FromResult(new ChatCompletionResponse(
                noContextAnswer,
                "fake-chat-premium-v1",
                "fake",
                CreateUsage(request, noContextAnswer)));
        }

        var bestBlock = SelectBestBlock(blocks, expandedQuestion);
        var answer = FormatStructuredAnswer(bestBlock.Content, answerIntent, language, bestBlock.Index);

        return Task.FromResult(new ChatCompletionResponse(
            answer,
            "fake-chat-premium-v1",
            "fake",
            CreateUsage(request, answer)));
    }

    private static ChatCompletionUsage CreateUsage(ChatCompletionRequest request, string answer)
    {
        var promptTokens = EstimateTokens(string.Join(Environment.NewLine, request.Messages.Select(static message => message.Content)));
        var completionTokens = EstimateTokens(answer);
        return new ChatCompletionUsage(promptTokens, completionTokens, promptTokens + completionTokens);
    }

    private static int EstimateTokens(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return 0;
        }

        return Math.Max(1, (int)Math.Ceiling(text.Length / 4d));
    }

    private static List<ContextBlock> ParseBlocks(string context)
    {
        var blocks = new List<ContextBlock>();
        foreach (Match match in BlockRegex.Matches(context))
        {
            var index = int.Parse(match.Groups[1].Value);
            var blockBody = match.Groups[2].Value.Trim();
            var content = ExtractSection(blockBody, "Content:");

            if (!string.IsNullOrWhiteSpace(content))
            {
                blocks.Add(new ContextBlock(index, content));
            }
        }

        return blocks;
    }

    private static ContextBlock SelectBestBlock(IReadOnlyList<ContextBlock> blocks, ExpandedSearchQuery question)
    {
        var bestBlock = blocks[0];
        var bestScore = Score(bestBlock.Content, question);

        foreach (var block in blocks.Skip(1))
        {
            var score = Score(block.Content, question);
            if (score > bestScore)
            {
                bestScore = score;
                bestBlock = block with { Score = score };
            }
        }

        return bestBlock with { Score = bestScore };
    }

    private static double Score(string content, ExpandedSearchQuery question)
    {
        if (question.TermWeights.Count == 0)
        {
            return 0;
        }

        var contentTokens = Tokenize(content);
        var weightedMatches = question.TermWeights
            .Where(entry => contentTokens.Contains(entry.Key))
            .Sum(static entry => entry.Value);

        var exactCoverage = question.OriginalTokens.Count == 0
            ? 0
            : (double)question.OriginalTokens.Count(contentTokens.Contains) / question.OriginalTokens.Count;

        return weightedMatches + (exactCoverage * 0.4d);
    }

    private static HashSet<string> Tokenize(string input)
    {
        return SemanticQueryExpander.Tokenize(input)
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string FormatStructuredAnswer(
        string content,
        ExpandedSearchQuery question,
        ChatLanguage language,
        int citationIndex)
    {
        if (IsSkillsQuestion(question))
        {
            var skills = ExtractInlineList(content, "skills", "habilidades", "competencias");
            if (skills.Length > 0)
            {
                return BuildStructuredAnswer(
                    language == ChatLanguage.Portuguese
                        ? $"As principais habilidades identificadas s\u00e3o {string.Join(", ", skills.Take(4))}"
                        : $"The key skills identified are {string.Join(", ", skills.Take(4))}",
                    language == ChatLanguage.Portuguese ? "Habilidades" : "Skills",
                    skills,
                    citationIndex);
            }
        }

        if (IsCompanyQuestion(question))
        {
            var currentCompany = ExtractCurrentCompany(content);
            if (!string.IsNullOrWhiteSpace(currentCompany))
            {
                return BuildStructuredAnswer(
                    language == ChatLanguage.Portuguese
                        ? $"A empresa atual identificada e {currentCompany}"
                        : $"The current company identified is {currentCompany}",
                    language == ChatLanguage.Portuguese ? "Empresa atual" : "Current company",
                    [currentCompany],
                    citationIndex);
            }
        }

        if (IsExperienceQuestion(question))
        {
            var experienceItems = ExtractSectionItems(
                content,
                "professional experience",
                "experience",
                "experiencia",
                "historico profissional");

            if (experienceItems.Length > 0)
            {
                return BuildStructuredAnswer(
                    language == ChatLanguage.Portuguese
                        ? "A experiencia profissional identificada esta resumida abaixo"
                        : "The professional experience identified is summarized below",
                    language == ChatLanguage.Portuguese ? "Experiencia profissional" : "Professional experience",
                    experienceItems,
                    citationIndex);
            }
        }

        if (IsEducationQuestion(question))
        {
            var educationItems = ExtractSectionItems(content, "education", "educacao", "formacao");
            if (educationItems.Length > 0)
            {
                return BuildStructuredAnswer(
                    language == ChatLanguage.Portuguese
                        ? "A formacao identificada esta resumida abaixo"
                        : "The education details identified are summarized below",
                    language == ChatLanguage.Portuguese ? "Formacao" : "Education",
                    educationItems,
                    citationIndex);
            }
        }

        if (IsDietQuestion(question))
        {
            var mealLine = ExtractMealLine(content, question);
            if (!string.IsNullOrWhiteSpace(mealLine))
            {
                return BuildStructuredAnswer(
                    language == ChatLanguage.Portuguese
                        ? $"A melhor opcao identificada e {mealLine}"
                        : $"The best grounded option is {mealLine}",
                    language == ChatLanguage.Portuguese ? "Recomendacao" : "Recommendation",
                    [mealLine],
                    citationIndex);
            }
        }

        if (IsTrainingQuestion(question))
        {
            var trainingLine = ExtractTrainingLine(content, question);
            if (!string.IsNullOrWhiteSpace(trainingLine))
            {
                return BuildStructuredAnswer(
                    language == ChatLanguage.Portuguese
                        ? $"O treino recomendado e {trainingLine}"
                        : $"The recommended training is {trainingLine}",
                    language == ChatLanguage.Portuguese ? "Treino" : "Training",
                    [trainingLine],
                    citationIndex);
            }
        }

        if (IsProcessQuestion(question.OriginalText))
        {
            var procedureItems = ExtractProcedureItems(content);
            if (procedureItems.Length > 0)
            {
                return BuildStructuredAnswer(
                    language == ChatLanguage.Portuguese
                        ? "O procedimento identificado esta resumido abaixo"
                        : "The grounded procedure is summarized below",
                    language == ChatLanguage.Portuguese ? "Passos" : "Steps",
                    procedureItems,
                    citationIndex);
            }
        }

        var genericSummary = SummarizeGeneric(content, question);
        return BuildStructuredAnswer(
            language == ChatLanguage.Portuguese
                ? $"A informacao mais relevante encontrada e {genericSummary}"
                : $"The most relevant information found is {genericSummary}",
            language == ChatLanguage.Portuguese ? "Resposta" : "Answer",
            [genericSummary],
            citationIndex);
    }

    private static string SummarizeGeneric(string content, ExpandedSearchQuery question)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        var sentences = Regex.Split(normalized, @"(?<=[.!?])\s+|\n+")
            .Select(static value => value.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        var sentence = sentences
            .Select((value, index) => new
            {
                Content = value,
                Score = Score(value, question),
                PositionBonus = Math.Max(0, 3 - index) * 0.25d
            })
            .OrderByDescending(static value => value.Score)
            .ThenByDescending(static value => value.PositionBonus)
            .ThenByDescending(static value => value.Content.Length)
            .Select(static value => value.Content)
            .FirstOrDefault() ?? normalized;

        return FinalizeSentence(sentence).TrimEnd('.');
    }

    private static string BuildStructuredAnswer(
        string directAnswer,
        string sectionLabel,
        IReadOnlyList<string> items,
        int citationIndex)
    {
        var builder = new StringBuilder();
        builder.AppendLine($"{FinalizeSentence(directAnswer)} [{citationIndex}]");

        if (items.Count > 0)
        {
            builder.AppendLine();
            builder.AppendLine($"- {sectionLabel}:");

            foreach (var item in items.Take(5))
            {
                builder.AppendLine($"  - {FinalizeSentence(item)} [{citationIndex}]");
            }
        }

        return builder.ToString().Trim();
    }

    private static string[] ExtractSectionItems(string content, params string[] labels)
    {
        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        var normalizedLabels = labels
            .SelectMany(SemanticQueryExpander.Tokenize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var items = new List<string>();
        var collecting = false;

        foreach (var line in lines)
        {
            var normalizedLine = SemanticQueryExpander.NormalizeText(line);
            var lineTokens = SemanticQueryExpander.Tokenize(line);

            if (normalizedLabels.Any(label => normalizedLine.Contains(label, StringComparison.Ordinal)))
            {
                collecting = true;

                var inlineItems = ExtractInlineRemainder(line);
                if (inlineItems.Length > 0)
                {
                    items.AddRange(inlineItems);
                }

                continue;
            }

            if (!collecting)
            {
                continue;
            }

            if (line.EndsWith(":", StringComparison.Ordinal) && items.Count > 0)
            {
                break;
            }

            if (line.StartsWith("-", StringComparison.Ordinal))
            {
                items.Add(line.TrimStart('-', ' ').Trim());
                continue;
            }

            if (items.Count == 0 && lineTokens.Count > 0)
            {
                items.Add(line.Trim());
                continue;
            }

            if (items.Count > 0)
            {
                break;
            }
        }

        return items
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .Distinct(StringComparer.Ordinal)
            .ToArray();
    }

    private static string[] ExtractInlineList(string content, params string[] labels)
    {
        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var normalizedLine = SemanticQueryExpander.NormalizeText(line);
            if (!labels.Any(label => normalizedLine.Contains(SemanticQueryExpander.NormalizeText(label), StringComparison.Ordinal)))
            {
                continue;
            }

            var items = ExtractInlineRemainder(line);
            if (items.Length > 0)
            {
                return items;
            }
        }

        return Array.Empty<string>();
    }

    private static string[] ExtractInlineRemainder(string line)
    {
        var separatorIndex = line.IndexOf(":", StringComparison.Ordinal);
        if (separatorIndex < 0 || separatorIndex >= line.Length - 1)
        {
            return Array.Empty<string>();
        }

        var remainder = line[(separatorIndex + 1)..]
            .Replace(" and ", ", ", StringComparison.OrdinalIgnoreCase)
            .Replace(" e ", ", ", StringComparison.OrdinalIgnoreCase);

        return remainder
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static item => !string.IsNullOrWhiteSpace(item))
            .ToArray();
    }

    private static string ExtractCurrentCompany(string content)
    {
        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var normalizedLine = SemanticQueryExpander.NormalizeText(line);
            if (!normalizedLine.Contains("current company", StringComparison.Ordinal)
                && !normalizedLine.Contains("empresa atual", StringComparison.Ordinal))
            {
                continue;
            }

            var inlineItems = ExtractInlineRemainder(line);
            if (inlineItems.Length > 0)
            {
                return inlineItems[0];
            }
        }

        var experienceItems = ExtractSectionItems(content, "professional experience", "experience", "experiencia");
        foreach (var item in experienceItems)
        {
            var match = Regex.Match(
                item,
                @"\bat\s+(.+?)\s+(from|since|between|in)\b",
                RegexOptions.IgnoreCase);

            if (match.Success)
            {
                return match.Groups[1].Value.Trim();
            }
        }

        return string.Empty;
    }

    private static string ExtractMealLine(string content, ExpandedSearchQuery question)
    {
        var targetLabels = new List<string>();
        if (HasQuestionTerm(question, "breakfast", "cafe", "manha"))
        {
            targetLabels.AddRange(["breakfast", "cafe", "cafe da manha"]);
        }

        if (HasQuestionTerm(question, "lunch", "almoco"))
        {
            targetLabels.AddRange(["lunch", "almoco"]);
        }

        if (HasQuestionTerm(question, "dinner", "jantar", "supper"))
        {
            targetLabels.AddRange(["dinner", "jantar", "supper"]);
        }

        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        foreach (var line in lines)
        {
            var normalizedLine = SemanticQueryExpander.NormalizeText(line);
            if (targetLabels.Any(label => normalizedLine.StartsWith(label, StringComparison.Ordinal)))
            {
                var items = ExtractInlineRemainder(line);
                return items.Length > 0 ? string.Join(", ", items) : line.Trim();
            }
        }

        return string.Empty;
    }

    private static string ExtractTrainingLine(string content, ExpandedSearchQuery question)
    {
        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        var preferredLabels = new List<string>();
        if (HasQuestionTerm(question, "today", "hoje"))
        {
            preferredLabels.AddRange(["today", "hoje"]);
        }

        if (HasQuestionTerm(question, "monday", "segunda"))
        {
            preferredLabels.AddRange(["monday", "segunda", "today", "hoje"]);
        }

        if (HasQuestionTerm(question, "tuesday", "terca"))
        {
            preferredLabels.AddRange(["tuesday", "terca"]);
        }

        if (HasQuestionTerm(question, "wednesday", "quarta"))
        {
            preferredLabels.AddRange(["wednesday", "quarta"]);
        }

        if (HasQuestionTerm(question, "thursday", "quinta"))
        {
            preferredLabels.AddRange(["thursday", "quinta"]);
        }

        if (HasQuestionTerm(question, "friday", "sexta"))
        {
            preferredLabels.AddRange(["friday", "sexta"]);
        }

        foreach (var label in preferredLabels.Distinct(StringComparer.Ordinal))
        {
            var matchedLine = lines.FirstOrDefault(line =>
                SemanticQueryExpander.NormalizeText(line).StartsWith(label, StringComparison.Ordinal));

            if (!string.IsNullOrWhiteSpace(matchedLine))
            {
                var items = ExtractInlineRemainder(matchedLine);
                return items.Length > 0 ? string.Join(", ", items) : matchedLine.Trim();
            }
        }

        return lines.FirstOrDefault() ?? string.Empty;
    }

    private static bool IsSkillsQuestion(ExpandedSearchQuery question)
    {
        return HasQuestionTerm(question, "skill", "skills", "habilidade", "habilidades", "competencia", "competencias", "stack", "technology", "tools", "framework");
    }

    private static bool IsEducationQuestion(ExpandedSearchQuery question)
    {
        return HasQuestionTerm(
            question,
            "education",
            "academic background",
            "educacao",
            "formacao",
            "formacao academica",
            "degree",
            "graduacao",
            "course",
            "qualification");
    }

    private static bool IsExperienceQuestion(ExpandedSearchQuery question)
    {
        return HasQuestionTerm(
            question,
            "professional experience",
            "professional experiences",
            "work experience",
            "work history",
            "experience",
            "experiencias",
            "experiencia",
            "historico profissional",
            "history",
            "career",
            "job",
            "role",
            "employer");
    }

    private static bool IsCompanyQuestion(ExpandedSearchQuery question)
    {
        return HasQuestionTerm(question, "current company", "company", "empresa atual", "empresa", "employer", "current");
    }

    private static bool IsDietQuestion(ExpandedSearchQuery question)
    {
        return HasQuestionTerm(question, "breakfast", "lunch", "dinner", "almoco", "jantar", "meal", "food", "dieta", "diet");
    }

    private static bool IsTrainingQuestion(ExpandedSearchQuery question)
    {
        return HasQuestionTerm(question, "training", "treino", "workout", "exercise", "today", "hoje", "monday", "segunda");
    }

    private static bool HasQuestionTerm(ExpandedSearchQuery question, params string[] terms)
    {
        var originalTokens = question.OriginalTokens.ToHashSet(StringComparer.Ordinal);
        var normalizedQuestion = SemanticQueryExpander.NormalizeText(question.OriginalText);

        foreach (var term in terms)
        {
            var normalizedTerm = SemanticQueryExpander.NormalizeText(term);
            if (normalizedTerm.Contains(' ', StringComparison.Ordinal)
                && normalizedQuestion.Contains(normalizedTerm, StringComparison.Ordinal))
            {
                return true;
            }

            foreach (var token in SemanticQueryExpander.Tokenize(term))
            {
                if (originalTokens.Contains(token))
                {
                    return true;
                }
            }
        }

        return false;
    }

    private static string[] ExtractProcedureItems(string content)
    {
        return Regex.Split(content, @"(?<=[.!?])\s+|\n+")
            .Select(static sentence => sentence.Trim())
            .Where(static sentence => !string.IsNullOrWhiteSpace(sentence))
            .Take(3)
            .Select(FinalizeSentence)
            .ToArray();
    }

    private static string FinalizeSentence(string sentence)
    {
        var normalized = sentence.Trim();
        if (normalized.Length > 220)
        {
            normalized = normalized[..220].TrimEnd();
        }

        if (!normalized.EndsWith(".", StringComparison.Ordinal))
        {
            normalized += ".";
        }

        return normalized;
    }

    private static string ExtractSection(string input, string startMarker, string? endMarker = null)
    {
        var start = input.IndexOf(startMarker, StringComparison.Ordinal);
        if (start < 0)
        {
            return string.Empty;
        }

        start += startMarker.Length;
        var end = endMarker is null
            ? input.Length
            : input.IndexOf(endMarker, start, StringComparison.Ordinal);

        if (end < 0)
        {
            end = input.Length;
        }

        return input[start..end].Trim();
    }

    private static bool IsProcessQuestion(string question)
    {
        var normalized = question.TrimStart();
        return normalized.StartsWith("how do", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("how can", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("how should", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("how to", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record ContextBlock(int Index, string Content, double Score = 0);
}
