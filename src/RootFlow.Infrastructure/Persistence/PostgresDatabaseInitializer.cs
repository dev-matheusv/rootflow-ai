using Microsoft.Extensions.Options;
using Npgsql;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly RootFlowOptions _options;

    public PostgresDatabaseInitializer(
        NpgsqlDataSource dataSource,
        IOptions<RootFlowOptions> options)
    {
        _dataSource = dataSource;
        _options = options.Value;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        const string schemaSql = """
                                 CREATE EXTENSION IF NOT EXISTS vector;

                                 CREATE TABLE IF NOT EXISTS workspaces (
                                     id uuid PRIMARY KEY,
                                     name text NOT NULL,
                                     slug text NOT NULL UNIQUE,
                                     created_at_utc timestamptz NOT NULL,
                                     is_active boolean NOT NULL
                                 );

                                 CREATE TABLE IF NOT EXISTS knowledge_documents (
                                     id uuid PRIMARY KEY,
                                     workspace_id uuid NOT NULL REFERENCES workspaces(id),
                                     original_file_name text NOT NULL,
                                     content_type text NOT NULL,
                                     size_bytes bigint NOT NULL,
                                     storage_path text NOT NULL,
                                     checksum text NOT NULL,
                                     extracted_text text NULL,
                                     status text NOT NULL,
                                     created_at_utc timestamptz NOT NULL,
                                     processed_at_utc timestamptz NULL,
                                     failure_reason text NULL
                                 );

                                 CREATE TABLE IF NOT EXISTS document_chunks (
                                     id uuid PRIMARY KEY,
                                     workspace_id uuid NOT NULL REFERENCES workspaces(id),
                                     document_id uuid NOT NULL REFERENCES knowledge_documents(id) ON DELETE CASCADE,
                                     sequence integer NOT NULL,
                                     content text NOT NULL,
                                     embedding vector(1536) NULL,
                                     token_count integer NOT NULL,
                                     source_label text NOT NULL,
                                     created_at_utc timestamptz NOT NULL
                                 );

                                 CREATE TABLE IF NOT EXISTS conversations (
                                     id uuid PRIMARY KEY,
                                     workspace_id uuid NOT NULL REFERENCES workspaces(id),
                                     title text NOT NULL,
                                     created_at_utc timestamptz NOT NULL,
                                     updated_at_utc timestamptz NOT NULL
                                 );

                                 CREATE TABLE IF NOT EXISTS conversation_messages (
                                     id uuid PRIMARY KEY,
                                     conversation_id uuid NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
                                     role text NOT NULL,
                                     content text NOT NULL,
                                     model_name text NULL,
                                     created_at_utc timestamptz NOT NULL
                                 );

                                 CREATE INDEX IF NOT EXISTS ix_knowledge_documents_workspace_created
                                     ON knowledge_documents (workspace_id, created_at_utc DESC);

                                 CREATE INDEX IF NOT EXISTS ix_document_chunks_workspace_document_sequence
                                     ON document_chunks (workspace_id, document_id, sequence);

                                 CREATE INDEX IF NOT EXISTS ix_conversations_workspace_updated
                                     ON conversations (workspace_id, updated_at_utc DESC);

                                 CREATE INDEX IF NOT EXISTS ix_conversation_messages_conversation_created
                                     ON conversation_messages (conversation_id, created_at_utc);
                                 """;

        const string seedWorkspaceSql = """
                                         INSERT INTO workspaces (id, name, slug, created_at_utc, is_active)
                                         VALUES (@id, @name, @slug, @createdAtUtc, TRUE)
                                         ON CONFLICT (id) DO NOTHING;
                                         """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        await using (var schemaCommand = new NpgsqlCommand(schemaSql, connection))
        {
            await schemaCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var workspaceCommand = new NpgsqlCommand(seedWorkspaceSql, connection))
        {
            workspaceCommand.Parameters.AddWithValue("id", _options.DefaultWorkspaceId);
            workspaceCommand.Parameters.AddWithValue("name", _options.DefaultWorkspaceName);
            workspaceCommand.Parameters.AddWithValue("slug", _options.DefaultWorkspaceSlug);
            workspaceCommand.Parameters.AddWithValue("createdAtUtc", DateTime.UtcNow);

            await workspaceCommand.ExecuteNonQueryAsync(cancellationToken);
        }
    }
}
