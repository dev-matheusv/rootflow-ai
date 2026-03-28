using RootFlow.Domain.Knowledge;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IKnowledgeDocumentRepository
{
    Task AddAsync(KnowledgeDocument document, CancellationToken cancellationToken = default);

    Task<KnowledgeDocument?> GetByIdAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<KnowledgeDocument>> ListByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default);
}
