using Npgsql;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresWorkspaceAccessValidator : IWorkspaceAccessValidator
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWorkspaceAccessValidator(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<WorkspaceRole?> GetActiveRoleAsync(
        Guid workspaceId,
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        // Single indexed lookup (ix_workspace_memberships_workspace_user) joined to the user and workspace flags.
        const string sql = """
                           SELECT m.role
                           FROM workspace_memberships AS m
                           INNER JOIN app_users AS u ON u.id = m.user_id
                           INNER JOIN workspaces AS w ON w.id = m.workspace_id
                           WHERE m.workspace_id = @workspaceId
                             AND m.user_id = @userId
                             AND m.is_active
                             AND u.is_active
                             AND w.is_active
                           LIMIT 1;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("userId", userId);

        var value = await command.ExecuteScalarAsync(cancellationToken) as string;
        return Enum.TryParse<WorkspaceRole>(value, ignoreCase: true, out var role) ? role : null;
    }
}
