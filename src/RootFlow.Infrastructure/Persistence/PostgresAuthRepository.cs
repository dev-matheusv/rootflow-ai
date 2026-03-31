using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Auth.Dtos;
using RootFlow.Domain.Users;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresAuthRepository : IAuthRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresAuthRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<AppUser?> GetUserByNormalizedEmailAsync(
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  email,
                                  normalized_email,
                                  full_name,
                                  password_hash,
                                  created_at_utc,
                                  is_active
                           FROM app_users
                           WHERE normalized_email = @normalizedEmail;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("normalizedEmail", normalizedEmail);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            return null;
        }

        var user = new AppUser(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTime>(5));

        if (!reader.GetBoolean(6))
        {
            user.Deactivate();
        }

        return user;
    }

    public async Task<bool> WorkspaceSlugExistsAsync(
        string slug,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM workspaces
                               WHERE slug = @slug
                           );
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("slug", slug);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    public async Task CreateUserWorkspaceAsync(
        AppUser user,
        Workspace workspace,
        WorkspaceMembership membership,
        CancellationToken cancellationToken = default)
    {
        const string insertUserSql = """
                                     INSERT INTO app_users (
                                         id,
                                         email,
                                         normalized_email,
                                         full_name,
                                         password_hash,
                                         created_at_utc,
                                         is_active
                                     )
                                     VALUES (
                                         @id,
                                         @email,
                                         @normalizedEmail,
                                         @fullName,
                                         @passwordHash,
                                         @createdAtUtc,
                                         @isActive
                                     );
                                     """;

        const string insertWorkspaceSql = """
                                          INSERT INTO workspaces (
                                              id,
                                              name,
                                              slug,
                                              created_at_utc,
                                              is_active
                                          )
                                          VALUES (
                                              @id,
                                              @name,
                                              @slug,
                                              @createdAtUtc,
                                              @isActive
                                          );
                                          """;

        const string insertMembershipSql = """
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
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var userCommand = new NpgsqlCommand(insertUserSql, connection, transaction))
        {
            userCommand.Parameters.AddWithValue("id", user.Id);
            userCommand.Parameters.AddWithValue("email", user.Email);
            userCommand.Parameters.AddWithValue("normalizedEmail", user.NormalizedEmail);
            userCommand.Parameters.AddWithValue("fullName", user.FullName);
            userCommand.Parameters.AddWithValue("passwordHash", user.PasswordHash);
            userCommand.Parameters.AddWithValue("createdAtUtc", user.CreatedAtUtc);
            userCommand.Parameters.AddWithValue("isActive", user.IsActive);

            await userCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var workspaceCommand = new NpgsqlCommand(insertWorkspaceSql, connection, transaction))
        {
            workspaceCommand.Parameters.AddWithValue("id", workspace.Id);
            workspaceCommand.Parameters.AddWithValue("name", workspace.Name);
            workspaceCommand.Parameters.AddWithValue("slug", workspace.Slug);
            workspaceCommand.Parameters.AddWithValue("createdAtUtc", workspace.CreatedAtUtc);
            workspaceCommand.Parameters.AddWithValue("isActive", workspace.IsActive);

            await workspaceCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var membershipCommand = new NpgsqlCommand(insertMembershipSql, connection, transaction))
        {
            membershipCommand.Parameters.AddWithValue("id", membership.Id);
            membershipCommand.Parameters.AddWithValue("workspaceId", membership.WorkspaceId);
            membershipCommand.Parameters.AddWithValue("userId", membership.UserId);
            membershipCommand.Parameters.AddWithValue("role", membership.Role.ToString());
            membershipCommand.Parameters.AddWithValue("createdAtUtc", membership.CreatedAtUtc);
            membershipCommand.Parameters.AddWithValue("isActive", membership.IsActive);

            await membershipCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public Task<AuthSessionDto?> GetPrimarySessionAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT u.id,
                                  u.full_name,
                                  u.email,
                                  w.id,
                                  w.name,
                                  w.slug,
                                  m.role
                           FROM workspace_memberships AS m
                           INNER JOIN app_users AS u ON u.id = m.user_id
                           INNER JOIN workspaces AS w ON w.id = m.workspace_id
                           WHERE m.user_id = @userId
                             AND m.is_active = TRUE
                             AND u.is_active = TRUE
                             AND w.is_active = TRUE
                           ORDER BY CASE m.role
                                        WHEN 'Owner' THEN 1
                                        WHEN 'Admin' THEN 2
                                        ELSE 3
                                    END,
                                    m.created_at_utc,
                                    m.id
                           LIMIT 1;
                           """;

        return GetSessionAsync(sql, ConfigureSingleParameter, userId, cancellationToken);
    }

    public Task<AuthSessionDto?> GetSessionAsync(
        Guid userId,
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT u.id,
                                  u.full_name,
                                  u.email,
                                  w.id,
                                  w.name,
                                  w.slug,
                                  m.role
                           FROM workspace_memberships AS m
                           INNER JOIN app_users AS u ON u.id = m.user_id
                           INNER JOIN workspaces AS w ON w.id = m.workspace_id
                           WHERE m.user_id = @userId
                             AND m.workspace_id = @workspaceId
                             AND m.is_active = TRUE
                             AND u.is_active = TRUE
                             AND w.is_active = TRUE
                           LIMIT 1;
                           """;

        return GetSessionAsync(
            sql,
            static (command, state) =>
            {
                command.Parameters.AddWithValue("userId", state.userId);
                command.Parameters.AddWithValue("workspaceId", state.workspaceId);
            },
            (userId, workspaceId),
            cancellationToken);
    }

    private async Task<AuthSessionDto?> GetSessionAsync<TState>(
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

        return new AuthSessionDto(
            new AuthUserDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2)),
            new AuthWorkspaceDto(
                reader.GetGuid(3),
                reader.GetString(4),
                reader.GetString(5)),
            Enum.Parse<WorkspaceRole>(reader.GetString(6), ignoreCase: true));
    }

    private static void ConfigureSingleParameter(NpgsqlCommand command, Guid userId)
    {
        command.Parameters.AddWithValue("userId", userId);
    }
}
