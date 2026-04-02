using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Workspaces;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresWorkspaceInvitationRepository : IWorkspaceInvitationRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWorkspaceInvitationRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task CreateAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           INSERT INTO workspace_invitations (
                               id,
                               workspace_id,
                               email,
                               normalized_email,
                               role,
                               token_hash,
                               invited_by_user_id,
                               status,
                               created_at_utc,
                               expires_at_utc,
                               accepted_at_utc,
                               revoked_at_utc
                           )
                           VALUES (
                               @id,
                               @workspaceId,
                               @email,
                               @normalizedEmail,
                               @role,
                               @tokenHash,
                               @invitedByUserId,
                               @status,
                               @createdAtUtc,
                               @expiresAtUtc,
                               @acceptedAtUtc,
                               @revokedAtUtc
                           );
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        ConfigureParameters(command, invitation);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public Task<WorkspaceInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  email,
                                  role,
                                  token_hash,
                                  invited_by_user_id,
                                  status,
                                  created_at_utc,
                                  expires_at_utc,
                                  accepted_at_utc,
                                  revoked_at_utc
                           FROM workspace_invitations
                           WHERE token_hash = @tokenHash
                           LIMIT 1;
                           """;

        return GetSingleAsync(
            sql,
            static (command, state) => command.Parameters.AddWithValue("tokenHash", state),
            tokenHash.Trim(),
            cancellationToken);
    }

    public Task<WorkspaceInvitation?> GetPendingForWorkspaceEmailAsync(
        Guid workspaceId,
        string normalizedEmail,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  email,
                                  role,
                                  token_hash,
                                  invited_by_user_id,
                                  status,
                                  created_at_utc,
                                  expires_at_utc,
                                  accepted_at_utc,
                                  revoked_at_utc
                           FROM workspace_invitations
                           WHERE workspace_id = @workspaceId
                             AND normalized_email = @normalizedEmail
                             AND status = @pendingStatus
                           ORDER BY created_at_utc DESC
                           LIMIT 1;
                           """;

        return GetSingleAsync(
            sql,
            static (command, state) =>
            {
                command.Parameters.AddWithValue("workspaceId", state.Item1);
                command.Parameters.AddWithValue("normalizedEmail", state.Item2);
                command.Parameters.AddWithValue("pendingStatus", WorkspaceInvitationStatus.Pending.ToString());
            },
            (workspaceId, normalizedEmail.Trim().ToUpperInvariant()),
            cancellationToken);
    }

    public async Task UpdateAsync(WorkspaceInvitation invitation, CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE workspace_invitations
                           SET role = @role,
                               status = @status,
                               expires_at_utc = @expiresAtUtc,
                               accepted_at_utc = @acceptedAtUtc,
                               revoked_at_utc = @revokedAtUtc
                           WHERE id = @id;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        ConfigureParameters(command, invitation);

        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private async Task<WorkspaceInvitation?> GetSingleAsync<TState>(
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

        var invitation = new WorkspaceInvitation(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            Enum.Parse<WorkspaceRole>(reader.GetString(3), ignoreCase: true),
            reader.GetString(4),
            reader.GetGuid(5),
            reader.GetFieldValue<DateTime>(7),
            reader.GetFieldValue<DateTime>(8));

        var status = Enum.Parse<WorkspaceInvitationStatus>(reader.GetString(6), ignoreCase: true);
        var acceptedAtUtc = reader.IsDBNull(9) ? (DateTime?)null : reader.GetFieldValue<DateTime>(9);
        var revokedAtUtc = reader.IsDBNull(10) ? (DateTime?)null : reader.GetFieldValue<DateTime>(10);

        switch (status)
        {
            case WorkspaceInvitationStatus.Accepted when acceptedAtUtc.HasValue:
                invitation.Accept(acceptedAtUtc.Value);
                break;
            case WorkspaceInvitationStatus.Revoked when revokedAtUtc.HasValue:
                invitation.Revoke(revokedAtUtc.Value);
                break;
            case WorkspaceInvitationStatus.Expired:
                invitation.Expire(invitation.ExpiresAtUtc);
                break;
        }

        return invitation;
    }

    private static void ConfigureParameters(NpgsqlCommand command, WorkspaceInvitation invitation)
    {
        command.Parameters.AddWithValue("id", invitation.Id);
        command.Parameters.AddWithValue("workspaceId", invitation.WorkspaceId);
        command.Parameters.AddWithValue("email", invitation.Email);
        command.Parameters.AddWithValue("normalizedEmail", invitation.NormalizedEmail);
        command.Parameters.AddWithValue("role", invitation.Role.ToString());
        command.Parameters.AddWithValue("tokenHash", invitation.TokenHash);
        command.Parameters.AddWithValue("invitedByUserId", invitation.InvitedByUserId);
        command.Parameters.AddWithValue("status", invitation.Status.ToString());
        command.Parameters.AddWithValue("createdAtUtc", invitation.CreatedAtUtc);
        command.Parameters.AddWithValue("expiresAtUtc", invitation.ExpiresAtUtc);
        command.Parameters.AddWithValue("acceptedAtUtc", (object?)invitation.AcceptedAtUtc ?? DBNull.Value);
        command.Parameters.AddWithValue("revokedAtUtc", (object?)invitation.RevokedAtUtc ?? DBNull.Value);
    }
}
