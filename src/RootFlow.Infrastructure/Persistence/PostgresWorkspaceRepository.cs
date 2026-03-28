using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresWorkspaceRepository : IWorkspaceRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWorkspaceRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task AddAsync(Workspace workspace, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO workspaces (id, name, slug, created_at_utc, is_active)
                           VALUES (@id, @name, @slug, @createdAtUtc, @isActive);
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("id", workspace.Id);
        command.Parameters.AddWithValue("name", workspace.Name);
        command.Parameters.AddWithValue("slug", workspace.Slug);
        command.Parameters.AddWithValue("createdAtUtc", workspace.CreatedAtUtc);
        command.Parameters.AddWithValue("isActive", workspace.IsActive);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<bool> ExistsAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM workspaces
                               WHERE id = @workspaceId
                                 AND is_active = TRUE
                           );
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task<Workspace?> GetByIdAsync(Guid workspaceId, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id, name, slug, created_at_utc, is_active
                           FROM workspaces
                           WHERE id = @workspaceId;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var workspace = new Workspace(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTime>(3));

        if (!reader.GetBoolean(4))
        {
            workspace.Deactivate();
        }

        return workspace;
    }
}
