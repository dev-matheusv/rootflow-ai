namespace RootFlow.Application.Abstractions.Search;

public interface IKnowledgeSearchService
{
    Task<IReadOnlyList<KnowledgeSearchMatch>> SearchAsync(
        Guid workspaceId,
        float[] queryEmbedding,
        int maxResults,
        CancellationToken cancellationToken = default);
}
