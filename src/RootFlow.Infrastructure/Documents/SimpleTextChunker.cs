using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Documents;

public sealed class SimpleTextChunker : ITextChunker
{
    private readonly TextChunkingOptions _options;

    public SimpleTextChunker(IOptions<TextChunkingOptions> options)
    {
        _options = options.Value;
    }

    public IReadOnlyList<TextChunk> Chunk(string text)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(text);

        if (_options.ChunkSize <= 0)
        {
            throw new InvalidOperationException("Chunk size must be greater than zero.");
        }

        if (_options.ChunkOverlap < 0 || _options.ChunkOverlap >= _options.ChunkSize)
        {
            throw new InvalidOperationException("Chunk overlap must be zero or greater and smaller than chunk size.");
        }

        var normalizedText = text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();

        if (normalizedText.Length == 0)
        {
            return Array.Empty<TextChunk>();
        }

        var chunks = new List<TextChunk>();
        var start = 0;
        var sequence = 0;

        while (start < normalizedText.Length)
        {
            var maxLength = Math.Min(_options.ChunkSize, normalizedText.Length - start);
            var end = start + maxLength;

            if (end < normalizedText.Length)
            {
                var candidateBreak = normalizedText.LastIndexOfAny([' ', '\n', '\t'], end - 1, maxLength);
                if (candidateBreak > start + (_options.ChunkSize / 2))
                {
                    end = candidateBreak + 1;
                }
            }

            var content = normalizedText[start..end].Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(new TextChunk(
                    sequence,
                    content,
                    CountWords(content),
                    $"Chunk {sequence + 1}"));

                sequence++;
            }

            if (end >= normalizedText.Length)
            {
                break;
            }

            start = Math.Max(end - _options.ChunkOverlap, start + 1);
        }

        return chunks;
    }

    private static int CountWords(string content)
    {
        return content.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }
}
