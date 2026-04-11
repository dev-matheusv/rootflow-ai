using RootFlow.Domain.DocumentTemplates;

namespace RootFlow.Application.Abstractions.Persistence;

public interface IDocumentTemplateRepository
{
    Task AddAsync(DocumentTemplate template, CancellationToken cancellationToken = default);
    Task UpdateAsync(DocumentTemplate template, CancellationToken cancellationToken = default);
    Task<DocumentTemplate?> GetByIdAsync(Guid templateId, Guid workspaceId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DocumentTemplate>> ListByWorkspaceAsync(Guid workspaceId, CancellationToken cancellationToken = default);
    Task<bool> SlugExistsAsync(string slug, Guid workspaceId, CancellationToken cancellationToken = default);
}
