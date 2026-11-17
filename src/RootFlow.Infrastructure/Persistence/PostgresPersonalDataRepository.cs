using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Account;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresPersonalDataRepository : IPersonalDataRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPersonalDataRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<PersonalDataExport?> ExportAsync(Guid userId, DateTime generatedAtUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        ExportedProfile? profile = null;
        await using (var command = new NpgsqlCommand(
            "SELECT id, full_name, email, created_at_utc FROM app_users WHERE id = @userId;", connection))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            if (await reader.ReadAsync(cancellationToken))
            {
                profile = new ExportedProfile(reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTime>(3));
            }
        }

        if (profile is null)
        {
            return null;
        }

        var memberships = new List<ExportedMembership>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT w.name, m.role, m.is_active, m.created_at_utc
            FROM workspace_memberships AS m
            INNER JOIN workspaces AS w ON w.id = m.workspace_id
            WHERE m.user_id = @userId
            ORDER BY m.created_at_utc;
            """, connection))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                memberships.Add(new ExportedMembership(reader.GetString(0), reader.GetString(1), reader.GetBoolean(2), reader.GetFieldValue<DateTime>(3)));
            }
        }

        var conversations = new List<(Guid Id, string Workspace, string Title, DateTime CreatedAt)>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT c.id, w.name, c.title, c.created_at_utc
            FROM conversations AS c
            INNER JOIN workspaces AS w ON w.id = c.workspace_id
            WHERE c.created_by_user_id = @userId
            ORDER BY c.created_at_utc;
            """, connection))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                conversations.Add((reader.GetGuid(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTime>(3)));
            }
        }

        var messagesByConversation = new Dictionary<Guid, List<ExportedMessage>>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT m.conversation_id, m.role, m.content, m.created_at_utc
            FROM conversation_messages AS m
            INNER JOIN conversations AS c ON c.id = m.conversation_id
            WHERE c.created_by_user_id = @userId
            ORDER BY m.created_at_utc, m.id;
            """, connection))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                var conversationId = reader.GetGuid(0);
                if (!messagesByConversation.TryGetValue(conversationId, out var list))
                {
                    messagesByConversation[conversationId] = list = [];
                }

                list.Add(new ExportedMessage(reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTime>(3)));
            }
        }

        var attempts = new List<ExportedTrainingAttempt>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT w.name, tm.title, a.status, a.score, a.started_at_utc, a.completed_at_utc
            FROM training_attempts AS a
            INNER JOIN training_modules AS tm ON tm.id = a.module_id
            INNER JOIN workspaces AS w ON w.id = a.workspace_id
            WHERE a.user_id = @userId
            ORDER BY a.started_at_utc;
            """, connection))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                attempts.Add(new ExportedTrainingAttempt(
                    reader.GetString(0),
                    reader.GetString(1),
                    reader.GetString(2),
                    reader.IsDBNull(3) ? null : reader.GetInt32(3),
                    reader.GetFieldValue<DateTime>(4),
                    reader.IsDBNull(5) ? null : reader.GetFieldValue<DateTime>(5)));
            }
        }

        var certificates = new List<ExportedCertificate>();
        await using (var command = new NpgsqlCommand(
            """
            SELECT w.name, p.name, tc.code, tc.issued_at_utc
            FROM training_certificates AS tc
            INNER JOIN training_programs AS p ON p.id = tc.program_id
            INNER JOIN workspaces AS w ON w.id = tc.workspace_id
            WHERE tc.user_id = @userId
            ORDER BY tc.issued_at_utc;
            """, connection))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                certificates.Add(new ExportedCertificate(reader.GetString(0), reader.GetString(1), reader.GetString(2), reader.GetFieldValue<DateTime>(3)));
            }
        }

        return new PersonalDataExport(
            generatedAtUtc,
            profile,
            memberships,
            conversations
                .Select(c => new ExportedConversation(
                    c.Id,
                    c.Workspace,
                    c.Title,
                    c.CreatedAt,
                    messagesByConversation.TryGetValue(c.Id, out var messages) ? messages : []))
                .ToArray(),
            attempts,
            certificates);
    }

    // Workspaces this person owns, with a flag for "someone else is still an active member".
    private const string OwnedWorkspacesSql = """
        SELECT w.id,
               w.name,
               EXISTS (
                   SELECT 1 FROM workspace_memberships AS other
                   WHERE other.workspace_id = w.id
                     AND other.user_id <> @userId
                     AND other.is_active
               ) AS has_other_members
        FROM workspace_memberships AS m
        INNER JOIN workspaces AS w ON w.id = m.workspace_id
        WHERE m.user_id = @userId
          AND m.is_active
          AND m.role = 'Owner'
          AND w.is_active
        """;

    public async Task<AccountDeletionBlockers> GetDeletionBlockersAsync(Guid userId, DateTime asOfUtc, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);

        var shared = new List<string>();
        var soleOwned = new List<(Guid Id, string Name)>();
        await using (var command = new NpgsqlCommand(OwnedWorkspacesSql + " ORDER BY w.name;", connection))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                if (reader.GetBoolean(2))
                {
                    shared.Add(reader.GetString(1));
                }
                else
                {
                    soleOwned.Add((reader.GetGuid(0), reader.GetString(1)));
                }
            }
        }

        var paid = new List<string>();
        foreach (var workspace in soleOwned)
        {
            await using var command = new NpgsqlCommand(
                """
                SELECT EXISTS (
                    SELECT 1 FROM workspace_subscriptions
                    WHERE workspace_id = @workspaceId
                      AND status = 'Active'
                      AND provider_subscription_id IS NOT NULL
                      AND canceled_at_utc IS NULL
                      AND current_period_end_utc > @asOfUtc
                );
                """, connection);
            command.Parameters.AddWithValue("workspaceId", workspace.Id);
            command.Parameters.AddWithValue("asOfUtc", asOfUtc);
            if (await command.ExecuteScalarAsync(cancellationToken) is true)
            {
                paid.Add(workspace.Name);
            }
        }

        return new AccountDeletionBlockers(shared, paid);
    }

    public async Task<IReadOnlyList<string>> DeleteUserAsync(Guid userId, CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        // 0. Close workspaces only this person uses (shared ones were blocked before getting here).
        var soleOwnedIds = new List<Guid>();
        await using (var command = new NpgsqlCommand(
            "SELECT id FROM (" + OwnedWorkspacesSql + ") AS owned WHERE NOT has_other_members;", connection, transaction))
        {
            command.Parameters.AddWithValue("userId", userId);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                soleOwnedIds.Add(reader.GetGuid(0));
            }
        }

        var storagePaths = new List<string>();
        if (soleOwnedIds.Count > 0)
        {
            var ids = soleOwnedIds.ToArray();
            await using (var command = new NpgsqlCommand(
                "SELECT storage_path FROM knowledge_documents WHERE workspace_id = ANY(@ids);", connection, transaction))
            {
                command.Parameters.AddWithValue("ids", ids);
                await using var reader = await command.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
                {
                    storagePaths.Add(reader.GetString(0));
                }
            }

            foreach (var sql in new[]
            {
                "DELETE FROM knowledge_documents WHERE workspace_id = ANY(@ids);",
                "DELETE FROM conversations WHERE workspace_id = ANY(@ids);",
                "DELETE FROM document_templates WHERE workspace_id = ANY(@ids);",
                "DELETE FROM training_programs WHERE workspace_id = ANY(@ids);",
                "DELETE FROM workspace_invitations WHERE workspace_id = ANY(@ids);",
                "UPDATE workspaces SET is_active = FALSE WHERE id = ANY(@ids);"
            })
            {
                await using var command = new NpgsqlCommand(sql, connection, transaction);
                command.Parameters.AddWithValue("ids", ids);
                await command.ExecuteNonQueryAsync(cancellationToken);
            }
        }

        // 1. The person's conversations go with them (messages cascade). Otherwise they would turn
        //    into unowned conversations, which admins can see.
        await using (var command = new NpgsqlCommand(
            "DELETE FROM conversations WHERE created_by_user_id = @userId;", connection, transaction))
        {
            command.Parameters.AddWithValue("userId", userId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // 2. Training programs are company content: hand them to the workspace owner (FK is RESTRICT).
        await using (var command = new NpgsqlCommand(
            """
            UPDATE training_programs AS p
            SET created_by_user_id = owner.user_id
            FROM (
                SELECT DISTINCT ON (m.workspace_id) m.workspace_id, m.user_id
                FROM workspace_memberships AS m
                WHERE m.role = 'Owner' AND m.is_active AND m.user_id <> @userId
                ORDER BY m.workspace_id, m.created_at_utc
            ) AS owner
            WHERE p.workspace_id = owner.workspace_id
              AND p.created_by_user_id = @userId;
            """, connection, transaction))
        {
            command.Parameters.AddWithValue("userId", userId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        // 3. The user row; the rest cascades (or is set to null for billing history).
        await using (var command = new NpgsqlCommand(
            "DELETE FROM app_users WHERE id = @userId;", connection, transaction))
        {
            command.Parameters.AddWithValue("userId", userId);
            await command.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
        return storagePaths;
    }
}
