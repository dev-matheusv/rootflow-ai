namespace RootFlow.Application.Abstractions.Search;

public sealed record KnowledgeSearchMatch(
    Guid DocumentId,
    Guid ChunkId,
    string DocumentName,
    string SourceLabel,
    string Content,
    int Sequence,
    double Score,
    double VectorScore,
    double KeywordScore,
    IReadOnlyList<string> MatchedTerms);
