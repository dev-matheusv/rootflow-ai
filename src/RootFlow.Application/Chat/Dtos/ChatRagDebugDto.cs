namespace RootFlow.Application.Chat.Dtos;

public sealed record ChatRagDebugDto(
    string Query,
    string RetrievalQuery,
    int HistoryMessageCount,
    int RetrievedChunkCount,
    IReadOnlyList<int> UsedChunkRanks,
    IReadOnlyList<ChatRetrievedChunkDebugDto> RetrievedChunks);
