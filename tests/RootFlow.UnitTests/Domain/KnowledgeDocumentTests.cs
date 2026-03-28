using RootFlow.Domain.Knowledge;

namespace RootFlow.UnitTests.Domain;

public sealed class KnowledgeDocumentTests
{
    [Fact]
    public void Constructor_SetsUploadedStatusAndMetadata()
    {
        var createdAtUtc = new DateTime(2026, 3, 28, 12, 0, 0, DateTimeKind.Utc);

        var document = new KnowledgeDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "handbook.pdf",
            "application/pdf",
            1024,
            "workspace-1/handbook.pdf",
            "abc123",
            createdAtUtc);

        Assert.Equal(DocumentStatus.Uploaded, document.Status);
        Assert.Equal("handbook.pdf", document.OriginalFileName);
        Assert.Equal("application/pdf", document.ContentType);
        Assert.Equal(1024, document.SizeBytes);
        Assert.Equal(createdAtUtc, document.CreatedAtUtc);
        Assert.Null(document.ProcessedAtUtc);
        Assert.Null(document.FailureReason);
    }

    [Fact]
    public void MarkProcessed_StoresExtractedTextAndUpdatesStatus()
    {
        var document = new KnowledgeDocument(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "faq.txt",
            "text/plain",
            128,
            "workspace-1/faq.txt",
            "hash",
            DateTime.UtcNow);

        var processedAtUtc = new DateTime(2026, 3, 28, 13, 30, 0, DateTimeKind.Utc);

        document.MarkProcessed("Frequently asked questions", processedAtUtc);

        Assert.Equal(DocumentStatus.Processed, document.Status);
        Assert.Equal("Frequently asked questions", document.ExtractedText);
        Assert.Equal(processedAtUtc, document.ProcessedAtUtc);
        Assert.Null(document.FailureReason);
    }
}
