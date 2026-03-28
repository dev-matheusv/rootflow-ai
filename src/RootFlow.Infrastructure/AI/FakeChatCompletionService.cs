using System.Text.RegularExpressions;
using RootFlow.Application.Abstractions.AI;

namespace RootFlow.Infrastructure.AI;

public sealed class FakeChatCompletionService : IChatCompletionService
{
    private static readonly Regex BlockRegex = new(
        @"\[(\d+)\]\s+[^\n]+\n(.*?)(?=\n\[\d+\]\s+[^\n]+\n|\nQuestion:|\z)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "at", "based", "be", "by", "do", "does", "for", "from", "how",
        "i", "in", "is", "it", "of", "on", "or", "that", "the", "to", "what", "when", "where",
        "which", "who", "why", "with", "you", "your"
    ];

    public Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = request.Messages.LastOrDefault()?.Content ?? string.Empty;
        var question = ExtractSection(prompt, "Question:");
        var context = ExtractSection(prompt, "Context Blocks:", "Question:");
        var blocks = ParseBlocks(context);

        if (blocks.Count == 0)
        {
            return Task.FromResult(new ChatCompletionResponse(
                "I do not know based on the current knowledge base.",
                "fake-chat-deterministic-v1"));
        }

        var bestBlock = SelectBestBlock(blocks, question);
        if (bestBlock.Score == 0)
        {
            return Task.FromResult(new ChatCompletionResponse(
                "I do not know based on the current knowledge base.",
                "fake-chat-deterministic-v1"));
        }

        var summary = Summarize(bestBlock.Content, question);
        var answer = $"Based on the knowledge base, {summary} [{bestBlock.Index}]";

        return Task.FromResult(new ChatCompletionResponse(answer, "fake-chat-deterministic-v1"));
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

    private static ContextBlock SelectBestBlock(IReadOnlyList<ContextBlock> blocks, string question)
    {
        var questionTokens = Tokenize(question);
        var bestBlock = blocks[0];
        var bestScore = Score(bestBlock.Content, questionTokens);

        foreach (var block in blocks.Skip(1))
        {
            var score = Score(block.Content, questionTokens);
            if (score > bestScore)
            {
                bestScore = score;
                bestBlock = block with { Score = score };
            }
        }

        return bestBlock with { Score = bestScore };
    }

    private static int Score(string content, HashSet<string> questionTokens)
    {
        if (questionTokens.Count == 0)
        {
            return 0;
        }

        var contentTokens = Tokenize(content);
        return questionTokens.Count(contentTokens.Contains);
    }

    private static HashSet<string> Tokenize(string input)
    {
        return TokenRegex.Matches(input.ToLowerInvariant())
            .Select(match => match.Value)
            .Where(static token => !StopWords.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Summarize(string content, string question)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        var sentences = normalized
            .Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .Select(static value => value.Trim())
            .Where(static value => !string.IsNullOrWhiteSpace(value))
            .ToArray();

        if (IsProcessQuestion(question) && sentences.Length > 1)
        {
            return FinalizeSentence(string.Join(". ", sentences.Take(2)));
        }

        var questionTokens = Tokenize(question);
        var sentence = sentences
            .Select((value, index) => new
            {
                Content = value,
                Score = Score(value, questionTokens),
                PositionBonus = Math.Max(0, 3 - index) * 0.25
            })
            .OrderByDescending(static value => value.Score + value.PositionBonus)
            .ThenByDescending(static value => value.Score)
            .ThenByDescending(static value => value.Content.Length)
            .Select(static value => value.Content)
            .FirstOrDefault() ?? normalized;

        return FinalizeSentence(sentence);
    }

    private static string FinalizeSentence(string sentence)
    {
        if (sentence.Length > 220)
        {
            sentence = sentence[..220].TrimEnd();
        }

        if (!sentence.EndsWith(".", StringComparison.Ordinal))
        {
            sentence += ".";
        }

        return sentence;
    }

    private static bool IsProcessQuestion(string question)
    {
        var normalized = question.TrimStart();
        return normalized.StartsWith("how do", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("how can", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("how should", StringComparison.OrdinalIgnoreCase)
            || normalized.StartsWith("how to", StringComparison.OrdinalIgnoreCase);
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

    private sealed record ContextBlock(int Index, string Content, int Score = 0);
}
