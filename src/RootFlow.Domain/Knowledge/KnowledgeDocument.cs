namespace RootFlow.Domain.Knowledge;

public sealed class KnowledgeDocument
{
    private KnowledgeDocument()
    {
    }

    public KnowledgeDocument(
        Guid id,
        Guid workspaceId,
        string originalFileName,
        string contentType,
        long sizeBytes,
        string storagePath,
        string checksum,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Document id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (sizeBytes < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sizeBytes), "File size cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(originalFileName);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        ArgumentException.ThrowIfNullOrWhiteSpace(storagePath);
        ArgumentException.ThrowIfNullOrWhiteSpace(checksum);

        Id = id;
        WorkspaceId = workspaceId;
        OriginalFileName = originalFileName.Trim();
        ContentType = contentType.Trim();
        SizeBytes = sizeBytes;
        StoragePath = storagePath.Trim();
        Checksum = checksum.Trim();
        CreatedAtUtc = createdAtUtc;
        Status = DocumentStatus.Uploaded;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public string OriginalFileName { get; private set; } = null!;

    public string ContentType { get; private set; } = null!;

    public long SizeBytes { get; private set; }

    public string StoragePath { get; private set; } = null!;

    public string Checksum { get; private set; } = null!;

    public string? ExtractedText { get; private set; }

    public DocumentStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? FailureReason { get; private set; }

    public void MarkProcessing()
    {
        Status = DocumentStatus.Processing;
        FailureReason = null;
    }

    public void MarkProcessed(string extractedText, DateTime processedAtUtc)
    {
        ArgumentNullException.ThrowIfNull(extractedText);

        ExtractedText = extractedText;
        ProcessedAtUtc = processedAtUtc;
        FailureReason = null;
        Status = DocumentStatus.Processed;
    }

    public void MarkFailed(string failureReason)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(failureReason);

        FailureReason = failureReason.Trim();
        Status = DocumentStatus.Failed;
    }
}
