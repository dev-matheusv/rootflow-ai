using System.Text.RegularExpressions;
using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.Search;

namespace RootFlow.Infrastructure.Search;

public sealed class PostgresKnowledgeSearchService : IKnowledgeSearchService
{
    private static readonly Regex TokenRegex = new("[a-z0-9]+", RegexOptions.Compiled | RegexOptions.IgnoreCase);
    private static readonly HashSet<string> StopWords =
    [
        "a", "an", "and", "are", "at", "be", "based", "by", "do", "does", "for", "from", "how",
        "i", "in", "is", "it", "of", "on", "or", "that", "the", "to", "what", "when", "where",
        "which", "who", "why", "with", "you", "your"
    ];

    private readonly NpgsqlDataSource _dataSource;

    public PostgresKnowledgeSearchService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<KnowledgeSearchMatch>> SearchAsync(
        Guid workspaceId,
        string queryText,
        float[] queryEmbedding,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding.Length == 0)
        {
            return Array.Empty<KnowledgeSearchMatch>();
        }

        const string sql = """
                           SELECT c.document_id,
                                  c.id,
                                  d.original_file_name,
                                  c.source_label,
                                  c.content,
                                  c.sequence,
                                  1 - (c.embedding <=> @embedding) AS vector_score
                           FROM document_chunks AS c
                           INNER JOIN knowledge_documents AS d ON d.id = c.document_id
                           WHERE c.workspace_id = @workspaceId
                             AND c.embedding IS NOT NULL
                             AND d.status = 'Processed'
                           ORDER BY c.embedding <=> @embedding
                           LIMIT @candidateCount;
                           """;

        var candidates = new List<SearchCandidate>();
        var candidateCount = Math.Max(maxResults * 8, 24);

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("embedding", new Vector(queryEmbedding));
        command.Parameters.AddWithValue("candidateCount", candidateCount);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            candidates.Add(new SearchCandidate(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                reader.GetDouble(6)));
        }

        return RankCandidates(queryText, candidates, Math.Max(1, maxResults));
    }

    private static IReadOnlyList<KnowledgeSearchMatch> RankCandidates(
        string queryText,
        IReadOnlyList<SearchCandidate> candidates,
        int maxResults)
    {
        var query = ParseQuery(queryText);
        var rankedCandidates = candidates
            .Select(candidate => RankCandidate(candidate, query))
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.KeywordScore)
            .ThenByDescending(static candidate => candidate.VectorScore)
            .ToArray();

        return Diversify(rankedCandidates, maxResults);
    }

    private static KnowledgeSearchMatch RankCandidate(SearchCandidate candidate, SearchQuery query)
    {
        var documentNameTokens = Tokenize(candidate.DocumentName);
        var sourceLabelTokens = Tokenize(candidate.SourceLabel);
        var contentTokens = Tokenize(candidate.Content);

        var matchedTerms = query.Tokens
            .Where(token =>
                documentNameTokens.Contains(token) ||
                sourceLabelTokens.Contains(token) ||
                contentTokens.Contains(token))
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var phraseSearchText = NormalizeForPhraseSearch(
            string.Join(' ', candidate.DocumentName, candidate.SourceLabel, candidate.Content));

        var phraseScore = query.Phrases.Length == 0
            ? 0
            : (double)query.Phrases.Count(phrase => phraseSearchText.Contains(phrase, StringComparison.Ordinal)) / query.Phrases.Length;

        var titleMatchCount = query.Tokens.Count(documentNameTokens.Contains);
        var sourceLabelMatchCount = query.Tokens.Count(sourceLabelTokens.Contains);
        var contentMatchCount = query.Tokens.Count(contentTokens.Contains);

        var tokenCoverage = query.Tokens.Length == 0
            ? 0
            : (double)matchedTerms.Length / query.Tokens.Length;

        var weightedTokenCoverage = query.Tokens.Length == 0
            ? 0
            : Math.Min(
                1d,
                ((titleMatchCount * 1.3) + (sourceLabelMatchCount * 1.1) + (contentMatchCount * 0.9)) /
                (query.Tokens.Length * 1.3));

        var keywordScore = query.Tokens.Length == 0 && query.Phrases.Length == 0
            ? 0
            : (tokenCoverage * 0.5) + (weightedTokenCoverage * 0.35) + (phraseScore * 0.15);

        var combinedScore = (candidate.VectorScore * 0.55) + (keywordScore * 0.35) + (phraseScore * 0.10);

        if (query.Tokens.Length > 0 && matchedTerms.Length == 0)
        {
            combinedScore *= 0.75;
        }

        return new KnowledgeSearchMatch(
            candidate.DocumentId,
            candidate.ChunkId,
            candidate.DocumentName,
            candidate.SourceLabel,
            candidate.Content,
            candidate.Sequence,
            combinedScore,
            candidate.VectorScore,
            keywordScore,
            matchedTerms);
    }

    private static IReadOnlyList<KnowledgeSearchMatch> Diversify(
        IReadOnlyList<KnowledgeSearchMatch> candidates,
        int maxResults)
    {
        var selected = new List<KnowledgeSearchMatch>(maxResults);
        var skipped = new List<KnowledgeSearchMatch>();
        var documentCounts = new Dictionary<Guid, int>();

        foreach (var candidate in candidates)
        {
            documentCounts.TryGetValue(candidate.DocumentId, out var count);
            if (count >= 2)
            {
                skipped.Add(candidate);
                continue;
            }

            selected.Add(candidate);
            documentCounts[candidate.DocumentId] = count + 1;

            if (selected.Count == maxResults)
            {
                return selected;
            }
        }

        foreach (var candidate in skipped)
        {
            selected.Add(candidate);
            if (selected.Count == maxResults)
            {
                break;
            }
        }

        return selected;
    }

    private static HashSet<string> Tokenize(string value)
    {
        return TokenRegex.Matches(value.ToLowerInvariant())
            .Select(static match => match.Value)
            .Where(static token => !StopWords.Contains(token))
            .ToHashSet(StringComparer.Ordinal);
    }

    private static SearchQuery ParseQuery(string queryText)
    {
        var tokens = TokenRegex.Matches(queryText.ToLowerInvariant())
            .Select(static match => match.Value)
            .Where(static token => !StopWords.Contains(token))
            .ToArray();

        var phrases = tokens.Length < 2
            ? []
            : tokens
                .Zip(tokens.Skip(1), static (left, right) => $"{left} {right}")
                .Distinct(StringComparer.Ordinal)
                .ToArray();

        return new SearchQuery(
            tokens.Distinct(StringComparer.Ordinal).ToArray(),
            phrases);
    }

    private static string NormalizeForPhraseSearch(string value)
    {
        return string.Join(' ', TokenRegex.Matches(value.ToLowerInvariant()).Select(static match => match.Value));
    }

    private sealed record SearchCandidate(
        Guid DocumentId,
        Guid ChunkId,
        string DocumentName,
        string SourceLabel,
        string Content,
        int Sequence,
        double VectorScore);

    private sealed record SearchQuery(
        string[] Tokens,
        string[] Phrases);
}
