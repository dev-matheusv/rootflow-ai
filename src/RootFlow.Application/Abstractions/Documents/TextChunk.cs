namespace RootFlow.Application.Abstractions.Documents;

public sealed record TextChunk(
    int Sequence,
    string Content,
    int TokenCount,
    string SourceLabel);
