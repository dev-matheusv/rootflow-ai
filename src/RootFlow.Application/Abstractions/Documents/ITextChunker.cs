namespace RootFlow.Application.Abstractions.Documents;

public interface ITextChunker
{
    IReadOnlyList<TextChunk> Chunk(string text);
}
