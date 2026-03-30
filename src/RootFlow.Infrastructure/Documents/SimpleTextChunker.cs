using System.Text;
using System.Text.RegularExpressions;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Documents;

public sealed class SimpleTextChunker : ITextChunker
{
    private static readonly Regex SentenceRegex = new(
        @"[^.!?\n]+(?:[.!?]+|$)",
        RegexOptions.Compiled | RegexOptions.Multiline);

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

        var normalizedText = Normalize(text);
        if (normalizedText.Length == 0)
        {
            return Array.Empty<TextChunk>();
        }

        var segments = BuildSegments(normalizedText);
        if (segments.Count == 0)
        {
            return Array.Empty<TextChunk>();
        }

        return BuildChunks(segments);
    }

    private IReadOnlyList<TextChunk> BuildChunks(IReadOnlyList<TextSegment> segments)
    {
        var chunks = new List<TextChunk>();
        var startSegmentIndex = 0;
        var sequence = 0;

        while (startSegmentIndex < segments.Count)
        {
            var buffer = new StringBuilder();
            var labels = new List<string>();
            var currentIndex = startSegmentIndex;

            while (currentIndex < segments.Count)
            {
                var segment = segments[currentIndex];
                var separatorLength = buffer.Length == 0 ? 0 : 2;
                if (buffer.Length > 0 && buffer.Length + separatorLength + segment.Content.Length > _options.ChunkSize)
                {
                    break;
                }

                if (buffer.Length > 0)
                {
                    buffer.AppendLine();
                    buffer.AppendLine();
                }

                buffer.Append(segment.Content);
                labels.Add(segment.SourceLabel);
                currentIndex++;
            }

            if (buffer.Length == 0)
            {
                var oversizedSegment = segments[currentIndex];
                buffer.Append(TrimToSentenceBoundary(oversizedSegment.Content, _options.ChunkSize));
                labels.Add(oversizedSegment.SourceLabel);
                currentIndex++;
            }

            var content = buffer.ToString().Trim();
            if (!string.IsNullOrWhiteSpace(content))
            {
                chunks.Add(new TextChunk(
                    sequence,
                    content,
                    CountWords(content),
                    BuildSourceLabel(labels, sequence)));
                sequence++;
            }

            if (currentIndex >= segments.Count)
            {
                break;
            }

            startSegmentIndex = GetOverlapStartIndex(segments, startSegmentIndex, currentIndex);
        }

        return chunks;
    }

    private int GetOverlapStartIndex(
        IReadOnlyList<TextSegment> segments,
        int chunkStartIndex,
        int chunkEndExclusiveIndex)
    {
        var overlapLength = 0;

        for (var index = chunkEndExclusiveIndex - 1; index > chunkStartIndex; index--)
        {
            overlapLength += segments[index].Content.Length + 2;
            if (overlapLength >= _options.ChunkOverlap)
            {
                return index;
            }
        }

        return Math.Max(chunkStartIndex + 1, chunkEndExclusiveIndex - 1);
    }

    private List<TextSegment> BuildSegments(string normalizedText)
    {
        var sections = normalizedText
            .Split("\n\n", StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var segments = new List<TextSegment>();
        var currentLabel = "Document";
        var paragraphIndex = 0;

        foreach (var section in sections)
        {
            var extracted = ExtractHeading(section, currentLabel);
            if (extracted.IsHeadingOnly)
            {
                currentLabel = extracted.Heading;
                continue;
            }

            var content = extracted.Content.Trim();
            if (string.IsNullOrWhiteSpace(content))
            {
                continue;
            }

            currentLabel = extracted.Heading;
            paragraphIndex++;

            var sourceLabel = BuildParagraphLabel(currentLabel, paragraphIndex);
            foreach (var segmentContent in SplitOversizedContent(content))
            {
                segments.Add(new TextSegment(segmentContent, sourceLabel));
            }
        }

        return segments;
    }

    private IReadOnlyList<string> SplitOversizedContent(string content)
    {
        if (content.Length <= _options.ChunkSize)
        {
            return [content];
        }

        var normalizedContent = Regex.Replace(content, @"\s+", " ").Trim();
        if (normalizedContent.Length <= _options.ChunkSize)
        {
            return [normalizedContent];
        }

        var sentences = SentenceRegex.Matches(normalizedContent)
            .Select(static match => match.Value.Trim())
            .Where(static sentence => !string.IsNullOrWhiteSpace(sentence))
            .ToArray();

        if (sentences.Length == 0)
        {
            return SplitIntoFixedWindows(normalizedContent);
        }

        var segments = new List<string>();
        var buffer = new StringBuilder();

        foreach (var sentence in sentences)
        {
            if (sentence.Length > _options.ChunkSize)
            {
                FlushBufferedSegment(segments, buffer);

                foreach (var window in SplitIntoFixedWindows(sentence))
                {
                    segments.Add(window);
                }

                continue;
            }

            var separatorLength = buffer.Length == 0 ? 0 : 1;
            if (buffer.Length > 0 && buffer.Length + separatorLength + sentence.Length > _options.ChunkSize)
            {
                FlushBufferedSegment(segments, buffer);
            }

            if (buffer.Length > 0)
            {
                buffer.Append(' ');
            }

            buffer.Append(sentence);
        }

        FlushBufferedSegment(segments, buffer);

        return segments.Count == 0
            ? SplitIntoFixedWindows(normalizedContent)
            : segments;
    }

    private IReadOnlyList<string> SplitIntoFixedWindows(string content)
    {
        var windows = new List<string>();
        var step = Math.Max(1, _options.ChunkSize - _options.ChunkOverlap);

        for (var start = 0; start < content.Length; start += step)
        {
            var length = Math.Min(_options.ChunkSize, content.Length - start);
            var window = content.Substring(start, length).Trim();
            if (string.IsNullOrWhiteSpace(window))
            {
                continue;
            }

            if (length == _options.ChunkSize)
            {
                var trimmedWindow = TrimToSentenceBoundary(window, _options.ChunkSize);
                if (!string.IsNullOrWhiteSpace(trimmedWindow))
                {
                    window = trimmedWindow;
                }
            }

            windows.Add(window);

            if (start + length >= content.Length)
            {
                break;
            }
        }

        return windows;
    }

    private static void FlushBufferedSegment(List<string> segments, StringBuilder buffer)
    {
        if (buffer.Length == 0)
        {
            return;
        }

        var value = buffer.ToString().Trim();
        if (!string.IsNullOrWhiteSpace(value))
        {
            segments.Add(value);
        }

        buffer.Clear();
    }

    private static HeadingExtraction ExtractHeading(string section, string currentLabel)
    {
        var lines = section
            .Split('\n', StringSplitOptions.TrimEntries)
            .Where(static line => !string.IsNullOrWhiteSpace(line))
            .ToArray();

        if (lines.Length == 0)
        {
            return new HeadingExtraction(currentLabel, string.Empty, false);
        }

        var firstLine = lines[0];
        if (!IsHeading(firstLine))
        {
            return new HeadingExtraction(currentLabel, string.Join(Environment.NewLine, lines), false);
        }

        var heading = CleanHeading(firstLine);
        var remainingLines = lines.Skip(1).ToArray();
        if (remainingLines.Length == 0)
        {
            return new HeadingExtraction(heading, string.Empty, true);
        }

        return new HeadingExtraction(heading, string.Join(Environment.NewLine, remainingLines), false);
    }

    private static bool IsHeading(string line)
    {
        if (line.StartsWith('#'))
        {
            return true;
        }

        if (line.Length > 80)
        {
            return false;
        }

        var letterCount = line.Count(char.IsLetter);
        if (letterCount == 0)
        {
            return false;
        }

        var upperCaseCount = line.Count(char.IsUpper);
        return upperCaseCount >= letterCount * 0.8;
    }

    private static string CleanHeading(string line)
    {
        return line.Trim().TrimStart('#').Trim();
    }

    private static string BuildParagraphLabel(string sectionLabel, int paragraphIndex)
    {
        return sectionLabel == "Document"
            ? $"Paragraph {paragraphIndex}"
            : $"{sectionLabel} - Paragraph {paragraphIndex}";
    }

    private static string BuildSourceLabel(IReadOnlyList<string> labels, int sequence)
    {
        var distinctLabels = labels
            .Where(static label => !string.IsNullOrWhiteSpace(label))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        if (distinctLabels.Length == 0)
        {
            return $"Chunk {sequence + 1}";
        }

        if (distinctLabels.Length == 1)
        {
            return distinctLabels[0];
        }

        return $"{distinctLabels[0]} (+{distinctLabels.Length - 1} more)";
    }

    private static string Normalize(string text)
    {
        return text
            .Replace("\r\n", "\n", StringComparison.Ordinal)
            .Replace('\r', '\n')
            .Trim();
    }

    private static string TrimToSentenceBoundary(string content, int maxLength)
    {
        if (content.Length <= maxLength)
        {
            return content;
        }

        var candidate = content[..maxLength];
        var sentenceEnd = candidate.LastIndexOfAny(['.', '!', '?', '\n']);
        if (sentenceEnd > maxLength / 2)
        {
            return candidate[..(sentenceEnd + 1)].Trim();
        }

        return candidate.Trim();
    }

    private static int CountWords(string content)
    {
        return content.Split([' ', '\n', '\t'], StringSplitOptions.RemoveEmptyEntries).Length;
    }

    private sealed record TextSegment(string Content, string SourceLabel);

    private sealed record HeadingExtraction(string Heading, string Content, bool IsHeadingOnly);
}
