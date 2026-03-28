namespace RootFlow.Infrastructure.Configuration;

public sealed class TextChunkingOptions
{
    public int ChunkSize { get; set; } = 1200;

    public int ChunkOverlap { get; set; } = 200;
}
