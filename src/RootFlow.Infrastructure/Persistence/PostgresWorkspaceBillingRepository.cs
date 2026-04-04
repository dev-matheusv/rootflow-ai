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
        WorkspaceCreditLedgerEntry? initialGrantEntry,
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
                                                 trial_ends_at_utc,
                                                 provider,
                                                 provider_customer_id,
                                                 provider_subscription_id,
                                                 provider_price_id,
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
                                                 @trialEndsAtUtc,
                                                 @provider,
                                                 @providerCustomerId,
                                                 @providerSubscriptionId,
                                                 @providerPriceId,
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

        if (!subscriptionExists && initialGrantEntry is not null)
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
                                  trial_ends_at_utc,
                                  provider,
                                  provider_customer_id,
                                  provider_subscription_id,
                                  provider_price_id,
                                  canceled_at_utc,
                                  created_at_utc,
                                  updated_at_utc
                           FROM workspace_subscriptions
                           WHERE workspace_id = @workspaceId
                             AND current_period_start_utc <= @asOfUtc
                             AND (
                                 (status = 'Active' AND current_period_end_utc > @asOfUtc)
                                 OR (status = 'Trial' AND COALESCE(trial_ends_at_utc, current_period_end_utc) > @asOfUtc)
                             )
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
                                  trial_ends_at_utc,
                                  provider,
                                  provider_customer_id,
                                  provider_subscription_id,
                                  provider_price_id,
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

    public async Task<WorkspaceSubscription?> GetSubscriptionByProviderSubscriptionIdAsync(
        string provider,
        string providerSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  billing_plan_id,
                                  status,
                                  current_period_start_utc,
                                  current_period_end_utc,
                                  trial_ends_at_utc,
                                  provider,
                                  provider_customer_id,
                                  provider_subscription_id,
                                  provider_price_id,
                                  canceled_at_utc,
                                  created_at_utc,
                                  updated_at_utc
                           FROM workspace_subscriptions
                           WHERE provider = @provider
                             AND provider_subscription_id = @providerSubscriptionId
                           ORDER BY updated_at_utc DESC, created_at_utc DESC, id DESC
                           LIMIT 1;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("providerSubscriptionId", providerSubscriptionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapSubscription(reader)
            : null;
    }

    public async Task UpdateSubscriptionAsync(
        WorkspaceSubscription subscription,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE workspace_subscriptions
                           SET billing_plan_id = @billingPlanId,
                               status = @status,
                               current_period_start_utc = @currentPeriodStartUtc,
                               current_period_end_utc = @currentPeriodEndUtc,
                               trial_ends_at_utc = @trialEndsAtUtc,
                               provider = @provider,
                               provider_customer_id = @providerCustomerId,
                               provider_subscription_id = @providerSubscriptionId,
                               provider_price_id = @providerPriceId,
                               canceled_at_utc = @canceledAtUtc,
                               updated_at_utc = @updatedAtUtc
                           WHERE id = @id;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        ConfigureSubscriptionParameters(command, subscription);
        await command.ExecuteNonQueryAsync(cancellationToken);
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

    public async Task<bool> LedgerReferenceExistsAsync(
        string referenceType,
        string referenceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT EXISTS (
                               SELECT 1
                               FROM workspace_credit_ledger
                               WHERE reference_type = @referenceType
                                 AND reference_id = @referenceId
                           );
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("referenceType", referenceType);
        command.Parameters.AddWithValue("referenceId", referenceId);

        var result = await command.ExecuteScalarAsync(cancellationToken);
        return result is true;
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

    public async Task AddBillingTransactionAsync(
        WorkspaceBillingTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(
            """
            INSERT INTO workspace_billing_transactions (
                id,
                workspace_id,
                provider,
                type,
                status,
                billing_plan_id,
                credit_amount,
                amount,
                currency_code,
                external_checkout_session_id,
                external_payment_intent_id,
                external_subscription_id,
                external_invoice_id,
                external_customer_id,
                created_at_utc,
                updated_at_utc,
                completed_at_utc
            )
            VALUES (
                @id,
                @workspaceId,
                @provider,
                @type,
                @status,
                @billingPlanId,
                @creditAmount,
                @amount,
                @currencyCode,
                @externalCheckoutSessionId,
                @externalPaymentIntentId,
                @externalSubscriptionId,
                @externalInvoiceId,
                @externalCustomerId,
                @createdAtUtc,
                @updatedAtUtc,
                @completedAtUtc
            );
            """,
            connection);

        ConfigureBillingTransactionParameters(command, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    public async Task<WorkspaceBillingTransaction?> GetBillingTransactionByCheckoutSessionIdAsync(
        string provider,
        string externalCheckoutSessionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  provider,
                                  type,
                                  status,
                                  billing_plan_id,
                                  credit_amount,
                                  amount,
                                  currency_code,
                                  external_checkout_session_id,
                                  external_payment_intent_id,
                                  external_subscription_id,
                                  external_invoice_id,
                                  external_customer_id,
                                  created_at_utc,
                                  updated_at_utc,
                                  completed_at_utc
                           FROM workspace_billing_transactions
                           WHERE provider = @provider
                             AND external_checkout_session_id = @externalCheckoutSessionId
                           LIMIT 1;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("externalCheckoutSessionId", externalCheckoutSessionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapBillingTransaction(reader)
            : null;
    }

    public async Task<WorkspaceBillingTransaction?> GetBillingTransactionByInvoiceIdAsync(
        string provider,
        string externalInvoiceId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  provider,
                                  type,
                                  status,
                                  billing_plan_id,
                                  credit_amount,
                                  amount,
                                  currency_code,
                                  external_checkout_session_id,
                                  external_payment_intent_id,
                                  external_subscription_id,
                                  external_invoice_id,
                                  external_customer_id,
                                  created_at_utc,
                                  updated_at_utc,
                                  completed_at_utc
                           FROM workspace_billing_transactions
                           WHERE provider = @provider
                             AND external_invoice_id = @externalInvoiceId
                           LIMIT 1;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("externalInvoiceId", externalInvoiceId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapBillingTransaction(reader)
            : null;
    }

    public async Task<WorkspaceBillingTransaction?> GetLatestBillingTransactionBySubscriptionIdAsync(
        string provider,
        string externalSubscriptionId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  workspace_id,
                                  provider,
                                  type,
                                  status,
                                  billing_plan_id,
                                  credit_amount,
                                  amount,
                                  currency_code,
                                  external_checkout_session_id,
                                  external_payment_intent_id,
                                  external_subscription_id,
                                  external_invoice_id,
                                  external_customer_id,
                                  created_at_utc,
                                  updated_at_utc,
                                  completed_at_utc
                           FROM workspace_billing_transactions
                           WHERE provider = @provider
                             AND external_subscription_id = @externalSubscriptionId
                           ORDER BY updated_at_utc DESC, created_at_utc DESC, id DESC
                           LIMIT 1;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("provider", provider);
        command.Parameters.AddWithValue("externalSubscriptionId", externalSubscriptionId);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapBillingTransaction(reader)
            : null;
    }

    public async Task UpdateBillingTransactionAsync(
        WorkspaceBillingTransaction transaction,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           UPDATE workspace_billing_transactions
                           SET status = @status,
                               billing_plan_id = @billingPlanId,
                               credit_amount = @creditAmount,
                               amount = @amount,
                               currency_code = @currencyCode,
                               external_checkout_session_id = @externalCheckoutSessionId,
                               external_payment_intent_id = @externalPaymentIntentId,
                               external_subscription_id = @externalSubscriptionId,
                               external_invoice_id = @externalInvoiceId,
                               external_customer_id = @externalCustomerId,
                               updated_at_utc = @updatedAtUtc,
                               completed_at_utc = @completedAtUtc
                           WHERE id = @id;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        ConfigureBillingTransactionParameters(command, transaction);
        await command.ExecuteNonQueryAsync(cancellationToken);
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
        command.Parameters.Add(new NpgsqlParameter("trialEndsAtUtc", NpgsqlDbType.TimestampTz)
        {
            Value = (object?)subscription.TrialEndsAtUtc ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("provider", NpgsqlDbType.Text)
        {
            Value = (object?)subscription.Provider ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("providerCustomerId", NpgsqlDbType.Text)
        {
            Value = (object?)subscription.ProviderCustomerId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("providerSubscriptionId", NpgsqlDbType.Text)
        {
            Value = (object?)subscription.ProviderSubscriptionId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("providerPriceId", NpgsqlDbType.Text)
        {
            Value = (object?)subscription.ProviderPriceId ?? DBNull.Value
        });
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

    private static void ConfigureBillingTransactionParameters(
        NpgsqlCommand command,
        WorkspaceBillingTransaction transaction)
    {
        command.Parameters.AddWithValue("id", transaction.Id);
        command.Parameters.AddWithValue("workspaceId", transaction.WorkspaceId);
        command.Parameters.AddWithValue("provider", transaction.Provider);
        command.Parameters.AddWithValue("type", transaction.Type.ToString());
        command.Parameters.AddWithValue("status", transaction.Status.ToString());
        command.Parameters.AddWithValue("amount", transaction.Amount);
        command.Parameters.AddWithValue("currencyCode", transaction.CurrencyCode);
        command.Parameters.AddWithValue("createdAtUtc", transaction.CreatedAtUtc);
        command.Parameters.AddWithValue("updatedAtUtc", transaction.UpdatedAtUtc);
        command.Parameters.Add(new NpgsqlParameter("billingPlanId", NpgsqlDbType.Uuid)
        {
            Value = (object?)transaction.BillingPlanId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("creditAmount", NpgsqlDbType.Bigint)
        {
            Value = (object?)transaction.CreditAmount ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("externalCheckoutSessionId", NpgsqlDbType.Text)
        {
            Value = (object?)transaction.ExternalCheckoutSessionId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("externalPaymentIntentId", NpgsqlDbType.Text)
        {
            Value = (object?)transaction.ExternalPaymentIntentId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("externalSubscriptionId", NpgsqlDbType.Text)
        {
            Value = (object?)transaction.ExternalSubscriptionId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("externalInvoiceId", NpgsqlDbType.Text)
        {
            Value = (object?)transaction.ExternalInvoiceId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("externalCustomerId", NpgsqlDbType.Text)
        {
            Value = (object?)transaction.ExternalCustomerId ?? DBNull.Value
        });
        command.Parameters.Add(new NpgsqlParameter("completedAtUtc", NpgsqlDbType.TimestampTz)
        {
            Value = (object?)transaction.CompletedAtUtc ?? DBNull.Value
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
            reader.GetFieldValue<DateTime>(12),
            reader.GetFieldValue<DateTime>(13),
            reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTime>(11),
            reader.IsDBNull(6) ? null : reader.GetFieldValue<DateTime>(6),
            reader.IsDBNull(7) ? null : reader.GetString(7),
            reader.IsDBNull(8) ? null : reader.GetString(8),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10));
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

    private static WorkspaceBillingTransaction MapBillingTransaction(NpgsqlDataReader reader)
    {
        return new WorkspaceBillingTransaction(
            reader.GetGuid(0),
            reader.GetGuid(1),
            reader.GetString(2),
            Enum.Parse<WorkspaceBillingTransactionType>(reader.GetString(3), ignoreCase: true),
            Enum.Parse<WorkspaceBillingTransactionStatus>(reader.GetString(4), ignoreCase: true),
            reader.GetFieldValue<decimal>(7),
            reader.GetString(8),
            reader.GetFieldValue<DateTime>(14),
            reader.GetFieldValue<DateTime>(15),
            reader.IsDBNull(5) ? null : reader.GetGuid(5),
            reader.IsDBNull(6) ? null : reader.GetInt64(6),
            reader.IsDBNull(9) ? null : reader.GetString(9),
            reader.IsDBNull(10) ? null : reader.GetString(10),
            reader.IsDBNull(11) ? null : reader.GetString(11),
            reader.IsDBNull(12) ? null : reader.GetString(12),
            reader.IsDBNull(13) ? null : reader.GetString(13),
            reader.IsDBNull(16) ? null : reader.GetFieldValue<DateTime>(16));
    }
}
