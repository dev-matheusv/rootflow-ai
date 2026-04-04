using Npgsql;
using NpgsqlTypes;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Billing;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresWorkspaceBillingRepository : IWorkspaceBillingRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresWorkspaceBillingRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task EnsureProvisionedAsync(
        WorkspaceSubscription subscription,
        WorkspaceCreditBalance balance,
        WorkspaceCreditLedgerEntry initialGrantEntry,
        CancellationToken cancellationToken = default)
    {
        const string subscriptionExistsSql = """
                                             SELECT EXISTS (
                                                 SELECT 1
                                                 FROM workspace_subscriptions
                                                 WHERE workspace_id = @workspaceId
                                             );
                                             """;

        const string balanceExistsSql = """
                                        SELECT EXISTS (
                                            SELECT 1
                                            FROM workspace_credit_balances
                                            WHERE workspace_id = @workspaceId
                                        );
                                        """;

        const string insertSubscriptionSql = """
                                             INSERT INTO workspace_subscriptions (
                                                 id,
                                                 workspace_id,
                                                 billing_plan_id,
                                                 status,
                                                 current_period_start_utc,
                                                 current_period_end_utc,
                                                 canceled_at_utc,
                                                 created_at_utc,
                                                 updated_at_utc
                                             )
                                             VALUES (
                                                 @id,
                                                 @workspaceId,
                                                 @billingPlanId,
                                                 @status,
                                                 @currentPeriodStartUtc,
                                                 @currentPeriodEndUtc,
                                                 @canceledAtUtc,
                                                 @createdAtUtc,
                                                 @updatedAtUtc
                                             );
                                             """;

        const string insertBalanceSql = """
                                        INSERT INTO workspace_credit_balances (
                                            workspace_id,
                                            available_credits,
                                            consumed_credits,
                                            updated_at_utc
                                        )
                                        VALUES (
                                            @workspaceId,
                                            @availableCredits,
                                            @consumedCredits,
                                            @updatedAtUtc
                                        );
                                        """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var subscriptionExists = await ExistsAsync(
            connection,
            transaction,
            subscriptionExistsSql,
            subscription.WorkspaceId,
            cancellationToken);

        var balanceExists = await ExistsAsync(
            connection,
            transaction,
            balanceExistsSql,
            balance.WorkspaceId,
            cancellationToken);

        if (!subscriptionExists)
        {
            await using var insertSubscriptionCommand = new NpgsqlCommand(insertSubscriptionSql, connection, transaction);
            ConfigureSubscriptionParameters(insertSubscriptionCommand, subscription);
            await insertSubscriptionCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!balanceExists)
        {
            await using var insertBalanceCommand = new NpgsqlCommand(insertBalanceSql, connection, transaction);
            ConfigureBalanceParameters(insertBalanceCommand, balance);
            await insertBalanceCommand.ExecuteNonQueryAsync(cancellationToken);
        }

        if (!subscriptionExists && initialGrantEntry.Amount > 0)
        {
            var currentBalance = await GetBalanceForUpdateAsync(
                connection,
                transaction,
                initialGrantEntry.WorkspaceId,
                cancellationToken);

            var nextBalance = new WorkspaceCreditBalance(
                currentBalance.WorkspaceId,
                currentBalance.AvailableCredits,
                currentBalance.ConsumedCredits,
                currentBalance.UpdatedAtUtc);

            nextBalance.GrantCredits(initialGrantEntry.Amount, initialGrantEntry.CreatedAtUtc);

            await UpdateBalanceAsync(connection, transaction, nextBalance, cancellationToken);
            await InsertLedgerEntryAsync(connection, transaction, initialGrantEntry, cancellationToken);
        }

        await transaction.CommitAsync(cancellationToken);
    }

    public async Task<WorkspaceSubscription?> GetCurrentSubscriptionAsync(
        Guid workspaceId,
        DateTime asOfUtc,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  billing_plan_id,
                                  status,
                                  current_period_start_utc,
                                  current_period_end_utc,
                                  canceled_at_utc,
                                  created_at_utc,
                                  updated_at_utc
                           FROM workspace_subscriptions
                           WHERE workspace_id = @workspaceId
                             AND status = 'Active'
                             AND current_period_start_utc <= @asOfUtc
                             AND current_period_end_utc > @asOfUtc
                           ORDER BY current_period_end_utc DESC, created_at_utc DESC, id DESC
                           LIMIT 1;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("asOfUtc", asOfUtc);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapSubscription(reader)
            : null;
    }

    public async Task<WorkspaceSubscription?> GetLatestSubscriptionAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  billing_plan_id,
                                  status,
                                  current_period_start_utc,
                                  current_period_end_utc,
                                  canceled_at_utc,
                                  created_at_utc,
                                  updated_at_utc
                           FROM workspace_subscriptions
                           WHERE workspace_id = @workspaceId
                           ORDER BY updated_at_utc DESC, created_at_utc DESC, id DESC
                           LIMIT 1;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapSubscription(reader)
            : null;
    }

    public async Task<WorkspaceCreditBalance?> GetCreditBalanceAsync(
        Guid workspaceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT workspace_id,
                                  available_credits,
                                  consumed_credits,
                                  updated_at_utc
                           FROM workspace_credit_balances
                           WHERE workspace_id = @workspaceId;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapBalance(reader)
            : null;
    }

    public async Task<WorkspaceCreditBalance> AppendLedgerEntryAsync(
        WorkspaceCreditLedgerEntry entry,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var updatedBalance = await ApplyLedgerEntryAsync(connection, transaction, entry, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return updatedBalance;
    }

    public async Task AddUsageEventAsync(
        WorkspaceUsageEvent usageEvent,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workspace_usage_events (
                id,
                workspace_id,
                user_id,
                conversation_id,
                provider,
                model,
                prompt_tokens,
                completion_tokens,
                total_tokens,
                estimated_cost,
                credits_charged,
                created_at_utc
            )
            VALUES (
                @id,
                @workspaceId,
                @userId,
                @conversationId,
                @provider,
                @model,
                @promptTokens,
                @completionTokens,
                @totalTokens,
                @estimatedCost,
                @creditsCharged,
                @createdAtUtc
            );
            """,
            connection);

        ConfigureUsageParameters(command, usageEvent);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<WorkspaceCreditBalance> RecordUsageAsync(
        WorkspaceUsageEvent usageEvent,
        WorkspaceCreditLedgerEntry debitEntry,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

        var updatedBalance = await ApplyLedgerEntryAsync(connection, transaction, debitEntry, cancellationToken);
        await InsertUsageEventAsync(connection, transaction, usageEvent, cancellationToken);

        await transaction.CommitAsync(cancellationToken);
        return updatedBalance;
    }

    public async Task<IReadOnlyList<WorkspaceCreditLedgerEntry>> ListLedgerEntriesAsync(
        Guid workspaceId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  type,
                                  amount,
                                  description,
                                  reference_type,
                                  reference_id,
                                  created_at_utc
                           FROM workspace_credit_ledger
                           WHERE workspace_id = @workspaceId
                           ORDER BY created_at_utc DESC, id DESC
                           LIMIT @take;
                           """;

        var entries = new List<WorkspaceCreditLedgerEntry>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("take", Math.Max(1, take));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            entries.Add(MapLedgerEntry(reader));
        }

        return entries;
    }

    public async Task<IReadOnlyList<WorkspaceUsageEvent>> ListUsageEventsAsync(
        Guid workspaceId,
        int take = 100,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  user_id,
                                  conversation_id,
                                  provider,
                                  model,
                                  prompt_tokens,
                                  completion_tokens,
                                  total_tokens,
                                  estimated_cost,
                                  credits_charged,
                                  created_at_utc
                           FROM workspace_usage_events
                           WHERE workspace_id = @workspaceId
                           ORDER BY created_at_utc DESC, id DESC
                           LIMIT @take;
                           """;

        var usageEvents = new List<WorkspaceUsageEvent>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("workspaceId", workspaceId);
        command.Parameters.AddWithValue("take", Math.Max(1, take));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            usageEvents.Add(MapUsageEvent(reader));
        }

        return usageEvents;
    }

    private static async Task<bool> ExistsAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        string sql,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(sql, connection, transaction);
        command.Parameters.AddWithValue("workspaceId", workspaceId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
    }

    private async Task<WorkspaceCreditBalance> ApplyLedgerEntryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkspaceCreditLedgerEntry entry,
        CancellationToken cancellationToken)
    {
        var currentBalance = await GetBalanceForUpdateAsync(connection, transaction, entry.WorkspaceId, cancellationToken);
        var updatedBalance = new WorkspaceCreditBalance(
            currentBalance.WorkspaceId,
            currentBalance.AvailableCredits,
            currentBalance.ConsumedCredits,
            currentBalance.UpdatedAtUtc);

        if (entry.Amount > 0)
        {
            updatedBalance.GrantCredits(entry.Amount, entry.CreatedAtUtc);
        }
        else
        {
            updatedBalance.ConsumeCredits(-entry.Amount, entry.CreatedAtUtc);
        }

        await UpdateBalanceAsync(connection, transaction, updatedBalance, cancellationToken);
        await InsertLedgerEntryAsync(connection, transaction, entry, cancellationToken);

        return updatedBalance;
    }

    private static async Task<WorkspaceCreditBalance> GetBalanceForUpdateAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        Guid workspaceId,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            SELECT workspace_id,
                   available_credits,
                   consumed_credits,
                   updated_at_utc
            FROM workspace_credit_balances
            WHERE workspace_id = @workspaceId
            FOR UPDATE;
            """,
            connection,
            transaction);

        command.Parameters.AddWithValue("workspaceId", workspaceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        if (!await reader.ReadAsync(cancellationToken))
        {
            throw new InvalidOperationException("Workspace billing balance was not found.");
        }

        return MapBalance(reader);
    }

    private static async Task UpdateBalanceAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkspaceCreditBalance balance,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            UPDATE workspace_credit_balances
            SET available_credits = @availableCredits,
                consumed_credits = @consumedCredits,
                updated_at_utc = @updatedAtUtc
            WHERE workspace_id = @workspaceId;
            """,
            connection,
            transaction);

        ConfigureBalanceParameters(command, balance);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertLedgerEntryAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkspaceCreditLedgerEntry entry,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workspace_credit_ledger (
                id,
                workspace_id,
                type,
                amount,
                description,
                reference_type,
                reference_id,
                created_at_utc
            )
            VALUES (
                @id,
                @workspaceId,
                @type,
                @amount,
                @description,
                @referenceType,
                @referenceId,
                @createdAtUtc
            );
            """,
            connection,
            transaction);

        ConfigureLedgerParameters(command, entry);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task InsertUsageEventAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        WorkspaceUsageEvent usageEvent,
        CancellationToken cancellationToken)
    {
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workspace_usage_events (
                id,
                workspace_id,
                user_id,
                conversation_id,
                provider,
                model,
                prompt_tokens,
                completion_tokens,
                total_tokens,
                estimated_cost,
                credits_charged,
                created_at_utc
            )
            VALUES (
                @id,
                @workspaceId,
                @userId,
                @conversationId,
                @provider,
                @model,
                @promptTokens,
                @completionTokens,
                @totalTokens,
                @estimatedCost,
                @creditsCharged,
                @createdAtUtc
            );
            """,
            connection,
            transaction);

        ConfigureUsageParameters(command, usageEvent);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static void ConfigureSubscriptionParameters(NpgsqlCommand command, WorkspaceSubscription subscription)
    {
        command.Parameters.AddWithValue("id", subscription.Id);
        command.Parameters.AddWithValue("workspaceId", subscription.WorkspaceId);
        command.Parameters.AddWithValue("billingPlanId", subscription.BillingPlanId);
        command.Parameters.AddWithValue("status", subscription.Status.ToString());
        command.Parameters.AddWithValue("currentPeriodStartUtc", subscription.CurrentPeriodStartUtc);
        command.Parameters.AddWithValue("currentPeriodEndUtc", subscription.CurrentPeriodEndUtc);
        command.Parameters.AddWithValue("createdAtUtc", subscription.CreatedAtUtc);
        command.Parameters.AddWithValue("updatedAtUtc", subscription.UpdatedAtUtc);
        command.Parameters.Add(new NpgsqlParameter("canceledAtUtc", NpgsqlDbType.TimestampTz)
        {
            Value = (object?)subscription.CanceledAtUtc ?? DBNull.Value
        });
    }

    private static void ConfigureBalanceParameters(NpgsqlCommand command, WorkspaceCreditBalance balance)
    {
        command.Parameters.AddWithValue("workspaceId", balance.WorkspaceId);
        command.Parameters.AddWithValue("availableCredits", balance.AvailableCredits);
        command.Parameters.AddWithValue("consumedCredits", balance.ConsumedCredits);
        command.Parameters.AddWithValue("updatedAtUtc", balance.UpdatedAtUtc);
    }

    private static void ConfigureLedgerParameters(NpgsqlCommand command, WorkspaceCreditLedgerEntry entry)
    {
        command.Parameters.AddWithValue("id", entry.Id);
        command.Parameters.AddWithValue("workspaceId", entry.WorkspaceId);
        command.Parameters.AddWithValue("type", entry.Type.ToString());
        command.Parameters.AddWithValue("amount", entry.Amount);
        command.Parameters.AddWithValue("description", entry.Description);
        command.Parameters.AddWithValue("createdAtUtc", entry.CreatedAtUtc);
        command.Parameters.Add(new NpgsqlParameter("referenceType", NpgsqlDbType.Text)
        {
            Value = (object?)entry.ReferenceType ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("referenceId", NpgsqlDbType.Text)
        {
            Value = (object?)entry.ReferenceId ?? DBNull.Value
        });
    }

    private static void ConfigureUsageParameters(NpgsqlCommand command, WorkspaceUsageEvent usageEvent)
    {
        command.Parameters.AddWithValue("id", usageEvent.Id);
        command.Parameters.AddWithValue("workspaceId", usageEvent.WorkspaceId);
        command.Parameters.AddWithValue("provider", usageEvent.Provider);
        command.Parameters.AddWithValue("model", usageEvent.Model);
        command.Parameters.AddWithValue("promptTokens", usageEvent.PromptTokens);
        command.Parameters.AddWithValue("completionTokens", usageEvent.CompletionTokens);
        command.Parameters.AddWithValue("totalTokens", usageEvent.TotalTokens);
        command.Parameters.AddWithValue("estimatedCost", usageEvent.EstimatedCost);
        command.Parameters.AddWithValue("creditsCharged", usageEvent.CreditsCharged);
        command.Parameters.AddWithValue("createdAtUtc", usageEvent.CreatedAtUtc);
        command.Parameters.Add(new NpgsqlParameter("userId", NpgsqlDbType.Uuid)
        {
            Value = (object?)usageEvent.UserId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("conversationId", NpgsqlDbType.Uuid)
        {
            Value = (object?)usageEvent.ConversationId ?? DBNull.Value
        });
    }

    private static WorkspaceSubscription MapSubscription(NpgsqlDataReader reader)
    {
        return new WorkspaceSubscription(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetGuid(2),
            Enum.Parse<WorkspaceSubscriptionStatus>(reader.GetString(3), ignoreCase: true),
            reader.GetFieldValue<DateTime>(4),
            reader.GetFieldValue<DateTime>(5),
            reader.GetFieldValue<DateTime>(7),
            reader.GetFieldValue<DateTime>(8),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTime>(6));
    }

    private static WorkspaceCreditBalance MapBalance(NpgsqlDataReader reader)
    {
        return new WorkspaceCreditBalance(
            reader.GetGuid(0),
            reader.GetInt64(1),
            reader.GetInt64(2),
            reader.GetFieldValue<DateTime>(3));
    }

    private static WorkspaceCreditLedgerEntry MapLedgerEntry(NpgsqlDataReader reader)
    {
        return new WorkspaceCreditLedgerEntry(
            reader.GetGuid(0),
            reader.GetGuid(1),
            Enum.Parse<WorkspaceCreditLedgerType>(reader.GetString(2), ignoreCase: true),
            reader.GetInt64(3),
            reader.GetString(4),
            reader.GetFieldValue<DateTime>(7),
            reader.IsDBNull(5) ? null : reader.GetString(5),
            reader.IsDBNull(6) ? null : reader.GetString(6));
    }

    private static WorkspaceUsageEvent MapUsageEvent(NpgsqlDataReader reader)
    {
        return new WorkspaceUsageEvent(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.IsDBNull(2) ? null : reader.GetGuid(2),
            reader.IsDBNull(3) ? null : reader.GetGuid(3),
            reader.GetString(4),
            reader.GetString(5),
            reader.GetInt32(6),
            reader.GetInt32(7),
            reader.GetInt32(8),
            reader.GetFieldValue<decimal>(9),
            reader.GetInt64(10),
            reader.GetFieldValue<DateTime>(11));
    }
}
