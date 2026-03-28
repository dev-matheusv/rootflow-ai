namespace RootFlow.Application.Chat.Dtos;

public sealed record ChatSourceDto(
    Guid DocumentId,
    Guid ChunkId,
    string DocumentName,
    string SourceLabel,
    string Excerpt,
    double Score);
