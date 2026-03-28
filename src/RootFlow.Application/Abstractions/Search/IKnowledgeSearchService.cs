namespace RootFlow.Application.Abstractions.Search;

public interface IKnowledgeSearchService
{
    Task<IReadOnlyList<KnowledgeSearchMatch>> SearchAsync(
        Guid workspaceId,
        string queryText,
        float[] queryEmbedding,
        int maxResults,
        CancellationToken cancellationToken = default);
}
