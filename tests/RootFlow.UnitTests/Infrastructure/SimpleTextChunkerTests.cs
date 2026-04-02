using Microsoft.Extensions.Options;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.Documents;

namespace RootFlow.UnitTests.Infrastructure;

public sealed class SimpleTextChunkerTests
{
    [Fact]
    public void Chunk_KeepsSectionContextAttached_AndSeparatesDistinctSections()
    {
        var chunker = new SimpleTextChunker(Options.Create(new TextChunkingOptions
        {
            ChunkSize = 220,
            ChunkOverlap = 40
        }));

        var text = """
                   # Resume

                   Professional Experience:
                   - Senior Software Engineer at Contoso leading internal API delivery.
                   - Built workflow automation for operations and finance teams.

                   Education:
                   - B.Sc. in Computer Science from UFRJ.
                   - Postgraduate certificate in Software Architecture.
                   """;

        var chunks = chunker.Chunk(text);

        var experienceChunk = Assert.Single(chunks, chunk => chunk.SourceLabel == "Professional Experience");
        Assert.StartsWith("Professional Experience:", experienceChunk.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Education:", experienceChunk.Content, StringComparison.Ordinal);

        var educationChunk = Assert.Single(chunks, chunk => chunk.SourceLabel == "Education");
        Assert.StartsWith("Education:", educationChunk.Content, StringComparison.Ordinal);
        Assert.DoesNotContain("Professional Experience:", educationChunk.Content, StringComparison.Ordinal);
    }

    [Fact]
    public void Chunk_RetainsHeadingContext_WhenLongSectionSpansMultipleChunks()
    {
        var chunker = new SimpleTextChunker(Options.Create(new TextChunkingOptions
        {
            ChunkSize = 180,
            ChunkOverlap = 45
        }));

        var text = """
                   # Travel Policy

                   Eligibility
                   Employees can book premium economy for flights longer than six hours with manager approval. Employees can book premium economy for flights longer than six hours with manager approval. Employees can book premium economy for flights longer than six hours with manager approval.
                   """;

        var chunks = chunker.Chunk(text);

        Assert.True(chunks.Count >= 2);
        Assert.All(chunks, chunk =>
        {
            Assert.Equal("Eligibility", chunk.SourceLabel);
            Assert.StartsWith("Eligibility:", chunk.Content, StringComparison.Ordinal);
        });
    }
}
