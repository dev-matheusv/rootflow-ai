using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.Search;

namespace RootFlow.Infrastructure.Search;

public sealed class PostgresKnowledgeSearchService : IKnowledgeSearchService
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresKnowledgeSearchService(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<IReadOnlyList<KnowledgeSearchMatch>> SearchAsync(
        Guid workspaceId,
        float[] queryEmbedding,
        int maxResults,
        CancellationToken cancellationToken = default)
    {
        if (queryEmbedding.Length == 0)
        {
            return Array.Empty<KnowledgeSearchMatch>();
        }

        const string sql = """
                           SELECT c.document_id,
                                  c.id,
                                  d.original_file_name,
                                  c.content,
                                  c.sequence,
                                  1 - (c.embedding <=> @embedding) AS score
                           FROM document_chunks AS c
                           INNER JOIN knowledge_documents AS d ON d.id = c.document_id
                           WHERE c.workspace_id = @workspaceId
                             AND c.embedding IS NOT NULL
                             AND d.status = 'Processed'
                           ORDER BY c.embedding <=> @embedding
                           LIMIT @maxResults;
                           """;

        var results = new List<KnowledgeSearchMatch>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("embedding", new Vector(queryEmbedding));
        command.Parameters.AddWithValue("maxResults", Math.Max(1, maxResults));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            results.Add(new KnowledgeSearchMatch(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetInt32(4),
                reader.GetDouble(5)));
        }

        return results;
    }
}
