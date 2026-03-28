namespace RootFlow.Domain.Knowledge;

public sealed class DocumentChunk
{
    private DocumentChunk()
    {
    }

    public DocumentChunk(
        Guid id,
        Guid workspaceId,
        Guid documentId,
        int sequence,
        string content,
        int tokenCount,
        string sourceLabel,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Chunk id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (documentId == Guid.Empty)
        {
            throw new ArgumentException("Document id cannot be empty.", nameof(documentId));
        }

        if (sequence < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(sequence), "Chunk sequence cannot be negative.");
        }

        if (tokenCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(tokenCount), "Token count cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(content);
        ArgumentException.ThrowIfNullOrWhiteSpace(sourceLabel);

        Id = id;
        WorkspaceId = workspaceId;
        DocumentId = documentId;
        Sequence = sequence;
        Content = content.Trim();
        TokenCount = tokenCount;
        SourceLabel = sourceLabel.Trim();
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid DocumentId { get; private set; }

    public int Sequence { get; private set; }

    public string Content { get; private set; } = null!;

    public float[]? Embedding { get; private set; }

    public int TokenCount { get; private set; }

    public string SourceLabel { get; private set; } = null!;

    public DateTime CreatedAtUtc { get; private set; }

    public void SetEmbedding(float[] embedding)
    {
        ArgumentNullException.ThrowIfNull(embedding);

        if (embedding.Length == 0)
        {
            throw new ArgumentException("Embedding cannot be empty.", nameof(embedding));
        }

        Embedding = [.. embedding];
    }
}
