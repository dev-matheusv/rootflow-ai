using RootFlow.Domain.Knowledge;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IKnowledgeDocumentRepository
{
    Task AddAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);

    Task UpdateAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);

    Task<KnowledgeDocument?> GetByIdAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    /// <summary>Deletes the document and, by cascade, its chunks and embeddings. Returns false when not found in the workspace.</summary>
    Task<bool> DeleteAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeDocument>> ListByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
