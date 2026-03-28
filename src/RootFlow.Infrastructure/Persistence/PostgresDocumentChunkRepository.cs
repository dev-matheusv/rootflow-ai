using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Knowledge;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresDocumentChunkRepository : IDocumentChunkRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDocumentChunkRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AddRangeAsync(
        IReadOnlyCollection<DocumentChunk> chunks,
        CancellationToken cancellationToken = default)
    {
        if (chunks.Count == 0)
        {
            return;
        }

        const string sql = """
                           INSERT INTO document_chunks (
                               id,
                               workspace_id,
                               document_id,
                               sequence,
                               content,
                               embedding,
                               token_count,
                               source_label,
                               created_at_utc
                           )
                           VALUES (
                               @id,
                               @workspaceId,
                               @documentId,
                               @sequence,
                               @content,
                               @embedding,
                               @tokenCount,
                               @sourceLabel,
                               @createdAtUtc
                           );
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        foreach (var chunk in chunks)
        {
            await using var command = new NpgsqlCommand(sql, connection, transaction);
            command.Parameters.AddWithValue("id", chunk.Id);
            command.Parameters.AddWithValue("workspaceId", chunk.WorkspaceId);
            command.Parameters.AddWithValue("documentId", chunk.DocumentId);
            command.Parameters.AddWithValue("sequence", chunk.Sequence);
            command.Parameters.AddWithValue("content", chunk.Content);
            command.Parameters.AddWithValue("embedding", chunk.Embedding is null ? DBNull.Value : new Vector(chunk.Embedding));
            command.Parameters.AddWithValue("tokenCount", chunk.TokenCount);
            command.Parameters.AddWithValue("sourceLabel", chunk.SourceLabel);
            command.Parameters.AddWithValue("createdAtUtc", chunk.CreatedAtUtc);

            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<DocumentChunk>> ListByDocumentIdAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  document_id,
                                  sequence,
                                  content,
                                  embedding,
                                  token_count,
                                  source_label,
                                  created_at_utc
                           FROM document_chunks
                           WHERE workspace_id = @workspaceId
                             AND document_id = @documentId
                           ORDER BY sequence;
                           """;

        var chunks = new List<DocumentChunk>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            var chunk = new DocumentChunk(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetInt32(3),
                reader.GetString(4),
                reader.GetInt32(6),
                reader.GetString(7),
                reader.GetFieldValue<DateTime>(8));

            if (!reader.IsDBNull(5))
            {
                var embedding = reader.GetFieldValue<Vector>(5);
                chunk.SetEmbedding(embedding.ToArray());
            }

            chunks.Add(chunk);
        }

        return chunks;
    }
}
