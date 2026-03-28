namespace RootFlow.Application.Abstractions.Search;

public sealed record KnowledgeSearchMatch(
    Guid DocumentId,
    Guid ChunkId,
    string DocumentName,
    string Content,
    int Sequence,
    double Score);
