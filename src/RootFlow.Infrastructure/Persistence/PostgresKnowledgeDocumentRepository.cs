using Npgsql;
using NpgsqlTypes;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Knowledge;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresKnowledgeDocumentRepository : IKnowledgeDocumentRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresKnowledgeDocumentRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AddAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO knowledge_documents (
                               id,
                               workspace_id,
                               original_file_name,
                               content_type,
                               size_bytes,
                               storage_path,
                               checksum,
                               extracted_text,
                               status,
                               created_at_utc,
                               processed_at_utc,
                               failure_reason
                           )
                           VALUES (
                               @id,
                               @workspaceId,
                               @originalFileName,
                               @contentType,
                               @sizeBytes,
                               @storagePath,
                               @checksum,
                               @extractedText,
                               @status,
                               @createdAtUtc,
                               @processedAtUtc,
                               @failureReason
                           );
                           """;

        await ExecuteAsync(sql, document, cancellationToken);
    }

    public async Task UpdateAsync(KnowledgeDocument document, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE knowledge_documents
                           SET original_file_name = @originalFileName,
                               content_type = @contentType,
                               size_bytes = @sizeBytes,
                               storage_path = @storagePath,
                               checksum = @checksum,
                               extracted_text = @extractedText,
                               status = @status,
                               processed_at_utc = @processedAtUtc,
                               failure_reason = @failureReason
                           WHERE id = @id
                             AND workspace_id = @workspaceId;
                           """;

        await ExecuteAsync(sql, document, cancellationToken);
    }

    public async Task<KnowledgeDocument?> GetByIdAsync(
        Guid workspaceId,
        Guid documentId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  original_file_name,
                                  content_type,
                                  size_bytes,
                                  storage_path,
                                  checksum,
                                  extracted_text,
                                  status,
                                  created_at_utc,
                                  processed_at_utc,
                                  failure_reason
                           FROM knowledge_documents
                           WHERE workspace_id = @workspaceId
                             AND id = @documentId;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("documentId", documentId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapDocument(reader)
            : null;
    }

    public async Task<IReadOnlyList<KnowledgeDocument>> ListByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  original_file_name,
                                  content_type,
                                  size_bytes,
                                  storage_path,
                                  checksum,
                                  extracted_text,
                                  status,
                                  created_at_utc,
                                  processed_at_utc,
                                  failure_reason
                           FROM knowledge_documents
                           WHERE workspace_id = @workspaceId
                           ORDER BY created_at_utc DESC;
                           """;

        var documents = new List<KnowledgeDocument>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            documents.Add(MapDocument(reader));
        }

        return documents;
    }

    private async Task ExecuteAsync(string sql, KnowledgeDocument document, CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", document.Id);
        command.Parameters.AddWithValue("workspaceId", document.WorkspaceId);
        command.Parameters.AddWithValue("originalFileName", document.OriginalFileName);
        command.Parameters.AddWithValue("contentType", document.ContentType);
        command.Parameters.AddWithValue("sizeBytes", document.SizeBytes);
        command.Parameters.AddWithValue("storagePath", document.StoragePath);
        command.Parameters.AddWithValue("checksum", document.Checksum);
        command.Parameters.AddWithValue("status", document.Status.ToString());
        command.Parameters.AddWithValue("createdAtUtc", document.CreatedAtUtc);
        command.Parameters.Add(new NpgsqlParameter("extractedText", NpgsqlDbType.Text)
        {
            Value = (object?)document.ExtractedText ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("processedAtUtc", NpgsqlDbType.TimestampTz)
        {
            Value = (object?)document.ProcessedAtUtc ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("failureReason", NpgsqlDbType.Text)
        {
            Value = (object?)document.FailureReason ?? DBNull.Value
        });

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static KnowledgeDocument MapDocument(NpgsqlDataReader reader)
    {
        var document = new KnowledgeDocument(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetString(3),
            reader.GetInt64(4),
            reader.GetString(5),
            reader.GetString(6),
            reader.GetFieldValue<DateTime>(9));

        var status = Enum.Parse<DocumentStatus>(reader.GetString(8), ignoreCase: true);
        var extractedText = reader.IsDBNull(7) ? null : reader.GetString(7);
        var processedAtUtc = reader.IsDBNull(10) ? (DateTime?)null : reader.GetFieldValue<DateTime>(10);
        var failureReason = reader.IsDBNull(11) ? null : reader.GetString(11);

        switch (status)
        {
            case DocumentStatus.Processing:
                document.MarkProcessing();
                break;
            case DocumentStatus.Processed:
                document.MarkProcessed(extractedText ?? string.Empty, processedAtUtc ?? document.CreatedAtUtc);
                break;
            case DocumentStatus.Failed:
                document.MarkFailed(failureReason ?? "Processing failed.");
                break;
        }

        return document;
    }
}
