namespace RootFlow.Application.Chat.Dtos;

public sealed record ChatRagDebugDto(
    string Query,
    int HistoryMessageCount,
    int RetrievedChunkCount,
    IReadOnlyList<ChatRetrievedChunkDebugDto> RetrievedChunks);
