using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresWorkspaceMembershipRepository : IWorkspaceMembershipRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWorkspaceMembershipRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public Task<WorkspaceMembership?> GetAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  user_id,
                                  role,
                                  created_at_utc,
                                  is_active
                           FROM workspace_memberships
                           WHERE workspace_id = @workspaceId
                             AND user_id = @userId
                           LIMIT 1;
                           """;

        return GetSingleAsync(
            sql,
            static (command, state) =>
            {
                command.Parameters.AddWithValue("workspaceId", state.workspaceId);
                command.Parameters.AddWithValue("userId", state.userId);
            },
            (workspaceId, userId),
            cancellationToken);
    }

    public async Task AddAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO workspace_memberships (
                               id,
                               workspace_id,
                               user_id,
                               role,
                               created_at_utc,
                               is_active
                           )
                           VALUES (
                               @id,
                               @workspaceId,
                               @userId,
                               @role,
                               @createdAtUtc,
                               @isActive
                           );
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        ConfigureParameters(command, membership);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task UpdateAsync(
        WorkspaceMembership membership,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE workspace_memberships
                           SET role = @role,
                               is_active = @isActive
                           WHERE id = @id;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        ConfigureParameters(command, membership);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<WorkspaceMemberRecord>> ListByWorkspaceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT m.user_id,
                                  u.full_name,
                                  u.email,
                                  m.role,
                                  m.created_at_utc,
                                  m.is_active
                           FROM workspace_memberships AS m
                           INNER JOIN app_users AS u ON u.id = m.user_id
                           WHERE m.workspace_id = @workspaceId
                           ORDER BY CASE m.role
                                        WHEN 'Owner' THEN 1
                                        WHEN 'Admin' THEN 2
                                        ELSE 3
                                    END,
                                    u.full_name,
                                    m.created_at_utc,
                                    m.id;
                           """;

        var members = new List<WorkspaceMemberRecord>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            members.Add(new WorkspaceMemberRecord(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                Enum.Parse<WorkspaceRole>(reader.GetString(3), ignoreCase: true),
                reader.GetFieldValue<DateTime>(4),
                reader.GetBoolean(5)));
        }

        return members;
    }

    private async Task<WorkspaceMembership?> GetSingleAsync<TState>(
        string sql,
        Action<NpgsqlCommand, TState> configureParameters,
        TState state,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        configureParameters(command, state);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var membership = new WorkspaceMembership(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            Enum.Parse<WorkspaceRole>(reader.GetString(3), ignoreCase: true),
            reader.GetFieldValue<DateTime>(4));

        if (!reader.GetBoolean(5))
        {
            membership.Deactivate();
        }

        return membership;
    }

    private static void ConfigureParameters(NpgsqlCommand command, WorkspaceMembership membership)
    {
        command.Parameters.AddWithValue("id", membership.Id);
        command.Parameters.AddWithValue("workspaceId", membership.WorkspaceId);
        command.Parameters.AddWithValue("userId", membership.UserId);
        command.Parameters.AddWithValue("role", membership.Role.ToString());
        command.Parameters.AddWithValue("createdAtUtc", membership.CreatedAtUtc);
        command.Parameters.AddWithValue("isActive", membership.IsActive);
    }
}
