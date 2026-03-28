using RootFlow.Domain.Knowledge;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IDocumentChunkRepository
{
    Task AddRangeAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<DocumentChunk>> ListByDocumentIdAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default);
}
