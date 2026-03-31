using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.Search;

namespace RootFlow.Infrastructure.Search;

public sealed class PostgresKnowledgeSearchService : IKnowledgeSearchService
{
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
        var candidateCount = Math.Max(maxResults * 12, 40);

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
        var query = SemanticQueryExpander.Expand(queryText);
        var rankedCandidates = candidates
            .Select(candidate => RankCandidate(candidate, query))
            .OrderByDescending(static candidate => candidate.Score)
            .ThenByDescending(static candidate => candidate.KeywordScore)
            .ThenByDescending(static candidate => candidate.PhraseScore)
            .ThenByDescending(static candidate => candidate.VectorScore)
            .ToArray();

        var relevantCandidates = SelectRelevantCandidates(rankedCandidates, maxResults);

        if (relevantCandidates.Count == 0)
        {
            return Array.Empty<KnowledgeSearchMatch>();
        }

        return Diversify(relevantCandidates, maxResults);
    }

    private static KnowledgeSearchMatch RankCandidate(SearchCandidate candidate, ExpandedSearchQuery query)
    {
        var documentNameTokens = SemanticQueryExpander.Tokenize(candidate.DocumentName).ToHashSet(StringComparer.Ordinal);
        var sourceLabelTokens = SemanticQueryExpander.Tokenize(candidate.SourceLabel).ToHashSet(StringComparer.Ordinal);
        var contentTokens = SemanticQueryExpander.Tokenize(candidate.Content).ToHashSet(StringComparer.Ordinal);

        var matchedTerms = new List<string>();
        var totalTermWeight = query.TermWeights.Values.Sum();
        var matchedWeight = 0d;
        var titleMatchWeight = 0d;
        var sourceLabelMatchWeight = 0d;
        var contentMatchWeight = 0d;

        foreach (var (term, weight) in query.TermWeights)
        {
            var termBoost = 0d;

            if (documentNameTokens.Contains(term))
            {
                titleMatchWeight += weight;
                termBoost = Math.Max(termBoost, 1.35d);
            }

            if (sourceLabelTokens.Contains(term))
            {
                sourceLabelMatchWeight += weight;
                termBoost = Math.Max(termBoost, 1.2d);
            }

            if (contentTokens.Contains(term))
            {
                contentMatchWeight += weight;
                termBoost = Math.Max(termBoost, 1d);
            }

            if (termBoost <= 0)
            {
                continue;
            }

            matchedTerms.Add(term);
            matchedWeight += weight * termBoost;
        }

        var phraseSearchText = NormalizeForPhraseSearch(
            string.Join(' ', candidate.DocumentName, candidate.SourceLabel, candidate.Content));

        var phraseScore = query.Phrases.Count == 0
            ? 0
            : (double)query.Phrases.Count(phrase => phraseSearchText.Contains(phrase, StringComparison.Ordinal)) / query.Phrases.Count;

        var originalMatchedTerms = query.OriginalTokens
            .Where(matchedTerms.Contains)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var exactCoverage = query.OriginalTokens.Count == 0
            ? 0
            : (double)originalMatchedTerms.Length / query.OriginalTokens.Count;

        var keywordScore = totalTermWeight == 0
            ? 0
            : Math.Min(1d, matchedWeight / (totalTermWeight * 1.35d));

        var fieldDensityScore = totalTermWeight == 0
            ? 0
            : Math.Min(
                1d,
                ((titleMatchWeight * 1.35d) + (sourceLabelMatchWeight * 1.15d) + contentMatchWeight) /
                (totalTermWeight * 1.35d));

        var combinedScore = (candidate.VectorScore * 0.50d)
            + (keywordScore * 0.24d)
            + (fieldDensityScore * 0.14d)
            + (exactCoverage * 0.07d)
            + (phraseScore * 0.05d);

        combinedScore += ComputeIntentBoost(query, documentNameTokens, sourceLabelTokens, contentTokens);
        combinedScore += ComputeStructuredContentBoost(candidate.Content, query);

        if (query.OriginalTokens.Count > 0 && originalMatchedTerms.Length == 0 && phraseScore == 0)
        {
            combinedScore *= 0.88d;
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
            phraseScore,
            matchedTerms);
    }

    private static IReadOnlyList<KnowledgeSearchMatch> SelectRelevantCandidates(
        IReadOnlyList<KnowledgeSearchMatch> rankedCandidates,
        int maxResults)
    {
        if (rankedCandidates.Count == 0)
        {
            return Array.Empty<KnowledgeSearchMatch>();
        }

        var topScore = rankedCandidates[0].Score;
        var topVector = rankedCandidates[0].VectorScore;

        var relevant = rankedCandidates
            .Where(candidate => IsRelevant(candidate, topScore, topVector))
            .ToArray();

        if (relevant.Length > 0)
        {
            return relevant;
        }

        if (rankedCandidates[0].MatchedTerms.Count > 0 || topScore >= 0.18d || topVector >= 0.24d)
        {
            return rankedCandidates
                .Take(Math.Min(maxResults, Math.Min(3, rankedCandidates.Count)))
                .ToArray();
        }

        return Array.Empty<KnowledgeSearchMatch>();
    }

    private static bool IsRelevant(
        KnowledgeSearchMatch candidate,
        double topScore,
        double topVector)
    {
        if (candidate.KeywordScore >= 0.10d || candidate.PhraseScore >= 0.10d)
        {
            return true;
        }

        if (candidate.VectorScore >= 0.36d && candidate.Score >= 0.24d)
        {
            return true;
        }

        return candidate.Score >= (topScore * 0.72d)
            && candidate.VectorScore >= Math.Max(0.28d, topVector * 0.78d);
    }

    private static double ComputeIntentBoost(
        ExpandedSearchQuery query,
        HashSet<string> documentNameTokens,
        HashSet<string> sourceLabelTokens,
        HashSet<string> contentTokens)
    {
        var boost = 0d;

        if (HasAnyQueryTerm(query, "resume", "cv", "curriculo", "curriculum", "experience", "experiencia", "employer", "company", "education", "educacao", "degree", "qualification", "skill", "stack", "technology"))
        {
            boost += ComputeFieldCueBoost(
                documentNameTokens,
                sourceLabelTokens,
                "resume", "cv", "curriculo", "curriculum", "experience", "experiencia", "education", "educacao", "skill", "qualification", "technology", "employer", "company", "job", "role");
        }

        if (HasAnyQueryTerm(query, "diet", "dieta", "meal", "food", "almoco", "lunch", "breakfast", "dinner"))
        {
            boost += ComputeFieldCueBoost(
                documentNameTokens,
                sourceLabelTokens,
                "diet", "dieta", "meal", "food", "nutrition", "almoco", "lunch", "breakfast", "dinner", "jantar", "cafe");
        }

        if (HasAnyQueryTerm(query, "training", "treino", "workout", "exercise", "fitness", "gym"))
        {
            boost += ComputeFieldCueBoost(
                documentNameTokens,
                sourceLabelTokens,
                "training", "treino", "workout", "exercise", "fitness", "gym", "strength", "cardio", "routine");
        }

        if (contentTokens.Count > 0 && query.OriginalTokens.Count > 0)
        {
            var directCoverage = (double)query.OriginalTokens.Count(contentTokens.Contains) / query.OriginalTokens.Count;
            boost += Math.Min(0.04d, directCoverage * 0.04d);
        }

        return Math.Min(0.16d, boost);
    }

    private static bool HasAnyQueryTerm(ExpandedSearchQuery query, params string[] terms)
    {
        return terms.Any(query.TermWeights.ContainsKey);
    }

    private static double ComputeFieldCueBoost(
        HashSet<string> documentNameTokens,
        HashSet<string> sourceLabelTokens,
        params string[] cueTokens)
    {
        var normalizedCues = cueTokens
            .SelectMany(SemanticQueryExpander.Tokenize)
            .Distinct(StringComparer.Ordinal)
            .ToArray();

        var documentCueMatches = normalizedCues.Count(documentNameTokens.Contains);
        var sourceCueMatches = normalizedCues.Count(sourceLabelTokens.Contains);

        return Math.Min(
            0.10d,
            (documentCueMatches * 0.025d) + (sourceCueMatches * 0.018d));
    }

    private static double ComputeStructuredContentBoost(
        string content,
        ExpandedSearchQuery query)
    {
        if (!HasAnyQueryTerm(
                query,
                "resume", "cv", "curriculo", "curriculum", "experience", "experiencia", "education",
                "educacao", "curso", "academico", "academica", "skill", "technology", "training", "treino", "diet", "meal", "food"))
        {
            return 0d;
        }

        var lines = content
            .Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

        var bulletLines = lines.Count(line => line.StartsWith("-", StringComparison.Ordinal));
        var labelLines = lines.Count(line => line.Contains(':', StringComparison.Ordinal));
        var shortStructuredLines = lines.Count(line => line.Length <= 120);

        var boost = (bulletLines * 0.012d) + (labelLines * 0.008d) + (shortStructuredLines * 0.002d);
        return Math.Min(0.06d, boost);
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

    private static string NormalizeForPhraseSearch(string value)
    {
        return string.Join(' ', SemanticQueryExpander.Tokenize(value));
    }

    private sealed record SearchCandidate(
        Guid DocumentId,
        Guid ChunkId,
        string DocumentName,
        string SourceLabel,
        string Content,
        int Sequence,
        double VectorScore);
}
