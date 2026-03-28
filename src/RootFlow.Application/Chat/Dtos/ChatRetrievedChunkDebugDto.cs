namespace RootFlow.Application.Chat.Dtos;

public sealed record ChatRetrievedChunkDebugDto(
    int Rank,
    Guid ChunkId,
    string DocumentName,
    string SourceLabel,
    int Sequence,
    double Score,
    double VectorScore,
    double KeywordScore,
    IReadOnlyList<string> MatchedTerms,
    string Reason);
