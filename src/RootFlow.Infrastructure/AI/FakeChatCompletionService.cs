using System.Text.RegularExpressions;
using RootFlow.Application.Abstractions.AI;

namespace RootFlow.Infrastructure.AI;

public sealed class FakeChatCompletionService : IChatCompletionService
{
    private static readonly Regex BlockRegex = new(
        @"\[(\d+)\]\s+[^\n]+\n(.*?)(?=\n\[\d+\]\s+|\nQuestion:|\z)",
        RegexOptions.Compiled | RegexOptions.Singleline);

    private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);

    public Task<ChatCompletionResponse> CompleteAsync(
        ChatCompletionRequest request,
        CancellationToken cancellationToken = default)
    {
        var prompt = request.Messages.LastOrDefault()?.Content ?? string.Empty;
        var question = ExtractSection(prompt, "Question:");
        var context = ExtractSection(prompt, "Context:", "Question:");
        var blocks = ParseBlocks(context);

        if (blocks.Count == 0)
        {
            return Task.FromResult(new ChatCompletionResponse(
                "I do not know based on the current knowledge base.",
                "fake-chat-deterministic-v1"));
        }

        var bestBlock = SelectBestBlock(blocks, question);
        var summary = Summarize(bestBlock.Content);
        var answer = $"Based on the knowledge base, {summary} [{bestBlock.Index}]";

        return Task.FromResult(new ChatCompletionResponse(answer, "fake-chat-deterministic-v1"));
    }

    private static List<ContextBlock> ParseBlocks(string context)
    {
        var blocks = new List<ContextBlock>();
        foreach (Match match in BlockRegex.Matches(context))
        {
            var index = int.Parse(match.Groups[1].Value);
            var content = match.Groups[2].Value.Trim();

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
                bestBlock = block;
            }
        }

        return bestBlock;
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
            .ToHashSet(StringComparer.Ordinal);
    }

    private static string Summarize(string content)
    {
        var normalized = content
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        var sentence = normalized
            .Split(['.', '\n'], StringSplitOptions.RemoveEmptyEntries)
            .FirstOrDefault()?.Trim() ?? normalized;

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

    private sealed record ContextBlock(int Index, string Content);
}
