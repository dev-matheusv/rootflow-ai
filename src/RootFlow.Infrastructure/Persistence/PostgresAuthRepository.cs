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

    public Task<AppUser?> GetUserByNormalizedEmailAsync(
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
                           WHERE normalized_email = @normalizedEmail
                           LIMIT 1;
                           """;

        return GetUserAsync(
            sql,
            static (command, state) => command.Parameters.AddWithValue("normalizedEmail", state),
            normalizedEmail,
            cancellationToken);
    }

    public Task<AppUser?> GetUserByIdAsync(
        Guid userId,
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
                           WHERE id = @userId
                           LIMIT 1;
                           """;

        return GetUserAsync(
            sql,
            static (command, state) => command.Parameters.AddWithValue("userId", state),
            userId,
            cancellationToken);
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

    public async Task StorePasswordResetTokenAsync(
        PasswordResetToken passwordResetToken,
        CancellationToken cancellationToken = default)
    {
        const string invalidateExistingSql = """
                                             UPDATE password_reset_tokens
                                             SET used_at_utc = @usedAtUtc
                                             WHERE user_id = @userId
                                               AND used_at_utc IS NULL;
                                             """;

        const string insertSql = """
                                 INSERT INTO password_reset_tokens (
                                     id,
                                     user_id,
                                     token_hash,
                                     created_at_utc,
                                     expires_at_utc,
                                     used_at_utc
                                 )
                                 VALUES (
                                     @id,
                                     @userId,
                                     @tokenHash,
                                     @createdAtUtc,
                                     @expiresAtUtc,
                                     @usedAtUtc
                                 );
                                 """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var invalidateCommand = new NpgsqlCommand(invalidateExistingSql, connection, transaction))
        {
            invalidateCommand.Parameters.AddWithValue("usedAtUtc", passwordResetToken.CreatedAtUtc);
            invalidateCommand.Parameters.AddWithValue("userId", passwordResetToken.UserId);
            await invalidateCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await using (var insertCommand = new NpgsqlCommand(insertSql, connection, transaction))
        {
            ConfigurePasswordResetTokenParameters(insertCommand, passwordResetToken);
            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public Task<PasswordResetToken?> GetPasswordResetTokenByHashAsync(
        string tokenHash,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  user_id,
                                  token_hash,
                                  created_at_utc,
                                  expires_at_utc,
                                  used_at_utc
                           FROM password_reset_tokens
                           WHERE token_hash = @tokenHash
                           LIMIT 1;
                           """;

        return GetPasswordResetTokenAsync(
            sql,
            static (command, state) => command.Parameters.AddWithValue("tokenHash", state),
            tokenHash,
            cancellationToken);
    }

    public async Task CompletePasswordResetAsync(
        Guid userId,
        Guid passwordResetTokenId,
        string passwordHash,
        DateTime completedAtUtc,
        CancellationToken cancellationToken = default)
    {
        const string updateUserSql = """
                                     UPDATE app_users
                                     SET password_hash = @passwordHash
                                     WHERE id = @userId
                                       AND is_active = TRUE;
                                     """;

        const string consumeTokenSql = """
                                       UPDATE password_reset_tokens
                                       SET used_at_utc = @completedAtUtc
                                       WHERE id = @passwordResetTokenId
                                         AND user_id = @userId
                                         AND used_at_utc IS NULL;
                                       """;

        const string consumeOtherTokensSql = """
                                             UPDATE password_reset_tokens
                                             SET used_at_utc = @completedAtUtc
                                             WHERE user_id = @userId
                                               AND id <> @passwordResetTokenId
                                               AND used_at_utc IS NULL;
                                             """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        await using (var updateUserCommand = new NpgsqlCommand(updateUserSql, connection, transaction))
        {
            updateUserCommand.Parameters.AddWithValue("passwordHash", passwordHash);
            updateUserCommand.Parameters.AddWithValue("userId", userId);
            var updatedUserCount = await updateUserCommand.ExecuteNonQueryAsync(cancellationToken);
            if (updatedUserCount != 1)
            {
                throw new InvalidOperationException("Password reset could not be completed.");
            }
        }

        await using (var consumeTokenCommand = new NpgsqlCommand(consumeTokenSql, connection, transaction))
        {
            consumeTokenCommand.Parameters.AddWithValue("completedAtUtc", completedAtUtc);
            consumeTokenCommand.Parameters.AddWithValue("passwordResetTokenId", passwordResetTokenId);
            consumeTokenCommand.Parameters.AddWithValue("userId", userId);
            var consumedTokenCount = await consumeTokenCommand.ExecuteNonQueryAsync(cancellationToken);
            if (consumedTokenCount != 1)
            {
                throw new InvalidOperationException("Password reset token is no longer active.");
            }
        }

        await using (var consumeOtherTokensCommand = new NpgsqlCommand(consumeOtherTokensSql, connection, transaction))
        {
            consumeOtherTokensCommand.Parameters.AddWithValue("completedAtUtc", completedAtUtc);
            consumeOtherTokensCommand.Parameters.AddWithValue("userId", userId);
            consumeOtherTokensCommand.Parameters.AddWithValue("passwordResetTokenId", passwordResetTokenId);
            await consumeOtherTokensCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    private async Task<AppUser?> GetUserAsync<TState>(
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
            Enum.Parse<WorkspaceRole>(reader.GetString(6), ignoreCase: true),
            false);
    }

    private async Task<PasswordResetToken?> GetPasswordResetTokenAsync<TState>(
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

        var passwordResetToken = new PasswordResetToken(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            reader.GetFieldValue<DateTime>(3),
            reader.GetFieldValue<DateTime>(4));

        if (!reader.IsDBNull(5))
        {
            passwordResetToken.MarkUsed(reader.GetFieldValue<DateTime>(5));
        }

        return passwordResetToken;
    }

    private static void ConfigureSingleParameter(NpgsqlCommand command, Guid userId)
    {
        command.Parameters.AddWithValue("userId", userId);
    }

    private static void ConfigurePasswordResetTokenParameters(
        NpgsqlCommand command,
        PasswordResetToken passwordResetToken)
    {
        command.Parameters.AddWithValue("id", passwordResetToken.Id);
        command.Parameters.AddWithValue("userId", passwordResetToken.UserId);
        command.Parameters.AddWithValue("tokenHash", passwordResetToken.TokenHash);
        command.Parameters.AddWithValue("createdAtUtc", passwordResetToken.CreatedAtUtc);
        command.Parameters.AddWithValue("expiresAtUtc", passwordResetToken.ExpiresAtUtc);
        command.Parameters.AddWithValue("usedAtUtc", (object?)passwordResetToken.UsedAtUtc ?? DBNull.Value);
    }
}
