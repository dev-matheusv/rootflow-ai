using Npgsql;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresDatabaseInitializer(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureMigrationsTableAsync(connection, cancellationToken);

        var appliedMigrationIds = await LoadAppliedMigrationIdsAsync(connection, cancellationToken);
        foreach (var migration in GetMigrations())
        {
            if (appliedMigrationIds.Contains(migration.Id))
            {
                continue;
            }

            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var migrationCommand = new NpgsqlCommand(migration.Sql, connection, transaction))
            {
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insertCommand = new NpgsqlCommand(
                             """
                             INSERT INTO schema_migrations (id, name, applied_at_utc)
                             VALUES (@id, @name, @appliedAtUtc);
                             """,
                             connection,
                             transaction))
            {
                insertCommand.Parameters.AddWithValue("id", migration.Id);
                insertCommand.Parameters.AddWithValue("name", migration.Name);
                insertCommand.Parameters.AddWithValue("appliedAtUtc", DateTime.UtcNow);

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
        }
    }

    private static async Task EnsureMigrationsTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS schema_migrations (
                               id text PRIMARY KEY,
                               name text NOT NULL,
                               applied_at_utc timestamptz NOT NULL
                           );
                           """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> LoadAppliedMigrationIdsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id
                           FROM schema_migrations;
                           """;

        var ids = new HashSet<string>(StringComparer.Ordinal);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static IReadOnlyList<DatabaseMigration> GetMigrations()
    {
        return
        [
            new DatabaseMigration(
                "202603280001_base_schema",
                "Create core RootFlow knowledge and conversation schema",
                """
                CREATE EXTENSION IF NOT EXISTS vector;

                CREATE TABLE IF NOT EXISTS workspaces (
                    id uuid PRIMARY KEY,
                    name text NOT NULL,
                    slug text NOT NULL,
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

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspaces_slug
                    ON workspaces (slug);

                CREATE INDEX IF NOT EXISTS ix_knowledge_documents_workspace_created
                    ON knowledge_documents (workspace_id, created_at_utc DESC);

                CREATE INDEX IF NOT EXISTS ix_document_chunks_workspace_document_sequence
                    ON document_chunks (workspace_id, document_id, sequence);

                CREATE INDEX IF NOT EXISTS ix_conversations_workspace_updated
                    ON conversations (workspace_id, updated_at_utc DESC);

                CREATE INDEX IF NOT EXISTS ix_conversation_messages_conversation_created
                    ON conversation_messages (conversation_id, created_at_utc);
                """),
            new DatabaseMigration(
                "202603310001_auth_multi_tenant_foundation",
                "Create users and workspace memberships for SaaS tenancy",
                """
                CREATE TABLE IF NOT EXISTS app_users (
                    id uuid PRIMARY KEY,
                    email text NOT NULL,
                    normalized_email text NOT NULL,
                    full_name text NOT NULL,
                    password_hash text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    is_active boolean NOT NULL
                );

                CREATE TABLE IF NOT EXISTS workspace_memberships (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                    role text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    is_active boolean NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_app_users_normalized_email
                    ON app_users (normalized_email);

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_memberships_workspace_user
                    ON workspace_memberships (workspace_id, user_id);

                CREATE INDEX IF NOT EXISTS ix_workspace_memberships_user_created
                    ON workspace_memberships (user_id, created_at_utc);
                """),
            new DatabaseMigration(
                "202604010001_workspace_invitation_foundation",
                "Create workspace invitations for future explicit shared-workspace membership",
                """
                CREATE TABLE IF NOT EXISTS workspace_invitations (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    email text NOT NULL,
                    normalized_email text NOT NULL,
                    role text NOT NULL,
                    token text NOT NULL,
                    invited_by_user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                    status text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    expires_at_utc timestamptz NOT NULL,
                    accepted_at_utc timestamptz NULL,
                    revoked_at_utc timestamptz NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_invitations_token
                    ON workspace_invitations (token);

                CREATE INDEX IF NOT EXISTS ix_workspace_invitations_workspace_email_status
                    ON workspace_invitations (workspace_id, normalized_email, status);

                CREATE INDEX IF NOT EXISTS ix_workspace_invitations_workspace_created
                    ON workspace_invitations (workspace_id, created_at_utc DESC);
                """)
        ];
    }

    private sealed record DatabaseMigration(string Id, string Name, string Sql);
}
