using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.PlatformAdmin.Dtos;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresPlatformAdminRepository : IPlatformAdminRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresPlatformAdminRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public async Task<PlatformAdminOverviewDto> GetOverviewAsync(
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT
                               (SELECT COUNT(*)::int
                                FROM workspaces
                                WHERE is_active = TRUE) AS total_workspaces,
                               (SELECT COUNT(*)::int
                                FROM workspace_subscriptions
                                WHERE status = 'Active') AS total_active_subscriptions,
                               (SELECT COUNT(*)::int
                                FROM workspace_subscriptions
                                WHERE status = 'Trial') AS total_trials,
                               (SELECT COUNT(*)::int
                                FROM app_users
                                WHERE is_active = TRUE) AS total_users,
                               COALESCE((SELECT SUM(available_credits)
                                         FROM workspace_credit_balances), 0) AS total_available_credits,
                               COALESCE((SELECT SUM(consumed_credits)
                                         FROM workspace_credit_balances), 0) AS total_consumed_credits,
                               COALESCE((SELECT SUM(estimated_cost)
                                         FROM workspace_usage_events), 0) AS estimated_provider_cost,
                               COALESCE((SELECT SUM(credits_charged)
                                         FROM workspace_usage_events), 0) AS total_credits_charged;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        await reader.ReadAsync(cancellationToken);

        var estimatedProviderCost = reader.GetDecimal(6);
        var estimatedRevenueBasis = ToRevenueBasis(reader.GetInt64(7), creditsPerCurrencyUnit);

        return new PlatformAdminOverviewDto(
            reader.GetInt32(0),
            reader.GetInt32(1),
            reader.GetInt32(2),
            reader.GetInt32(3),
            reader.GetInt64(4),
            reader.GetInt64(5),
            RoundMoney(estimatedProviderCost),
            estimatedRevenueBasis,
            RoundMoney(estimatedRevenueBasis - estimatedProviderCost));
    }

    public async Task<IReadOnlyList<PlatformAdminUsageWindowDto>> ListUsageWindowsAsync(
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           WITH windows AS (
                               SELECT '24h'::text AS key, NOW() - INTERVAL '24 hours' AS starts_at, 1 AS sort_order
                               UNION ALL
                               SELECT '7d'::text AS key, NOW() - INTERVAL '7 days' AS starts_at, 2 AS sort_order
                               UNION ALL
                               SELECT '30d'::text AS key, NOW() - INTERVAL '30 days' AS starts_at, 3 AS sort_order
                           )
                           SELECT windows.key,
                                  COUNT(DISTINCT usage_event.workspace_id)::int AS workspace_count,
                                  COUNT(usage_event.id)::int AS event_count,
                                  COALESCE(SUM(usage_event.prompt_tokens), 0) AS prompt_tokens,
                                  COALESCE(SUM(usage_event.completion_tokens), 0) AS completion_tokens,
                                  COALESCE(SUM(usage_event.total_tokens), 0) AS total_tokens,
                                  COALESCE(SUM(usage_event.credits_charged), 0) AS credits_charged,
                                  COALESCE(SUM(usage_event.estimated_cost), 0) AS estimated_provider_cost
                           FROM windows
                           LEFT JOIN workspace_usage_events AS usage_event
                               ON usage_event.created_at_utc >= windows.starts_at
                           GROUP BY windows.key, windows.sort_order
                           ORDER BY windows.sort_order;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var windows = new List<PlatformAdminUsageWindowDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var estimatedProviderCost = reader.GetDecimal(7);
            var estimatedRevenueBasis = ToRevenueBasis(reader.GetInt64(6), creditsPerCurrencyUnit);

            windows.Add(new PlatformAdminUsageWindowDto(
                reader.GetString(0),
                reader.GetInt32(1),
                reader.GetInt32(2),
                reader.GetInt64(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                reader.GetInt64(6),
                RoundMoney(estimatedProviderCost),
                estimatedRevenueBasis,
                RoundMoney(estimatedRevenueBasis - estimatedProviderCost)));
        }

        return windows;
    }

    public async Task<IReadOnlyList<PlatformAdminWorkspaceSummaryDto>> ListWorkspaceSummariesAsync(
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           WITH current_subscriptions AS (
                               SELECT DISTINCT ON (subscription.workspace_id)
                                      subscription.workspace_id,
                                      subscription.status,
                                      subscription.trial_ends_at_utc,
                                      billing_plan.name AS plan_name
                               FROM workspace_subscriptions AS subscription
                               LEFT JOIN billing_plans AS billing_plan ON billing_plan.id = subscription.billing_plan_id
                               ORDER BY subscription.workspace_id,
                                        CASE subscription.status
                                            WHEN 'Active' THEN 1
                                            WHEN 'Trial' THEN 2
                                            ELSE 3
                                        END,
                                        subscription.updated_at_utc DESC,
                                        subscription.created_at_utc DESC
                           ),
                           member_counts AS (
                               SELECT workspace_id,
                                      COUNT(*)::int AS member_count
                               FROM workspace_memberships
                               WHERE is_active = TRUE
                               GROUP BY workspace_id
                           ),
                           usage_rollup AS (
                               SELECT workspace_id,
                                      COALESCE(SUM(credits_charged), 0) AS credits_charged,
                                      COALESCE(SUM(total_tokens), 0) AS total_tokens,
                                      COALESCE(SUM(estimated_cost), 0) AS estimated_provider_cost,
                                      MAX(created_at_utc) AS last_usage_at_utc
                               FROM workspace_usage_events
                               GROUP BY workspace_id
                           )
                           SELECT workspace.id,
                                  workspace.name,
                                  workspace.slug,
                                  current_subscriptions.plan_name,
                                  COALESCE(current_subscriptions.status, 'None') AS subscription_status,
                                  COALESCE(member_counts.member_count, 0) AS member_count,
                                  COALESCE(balance.available_credits, 0) AS available_credits,
                                  COALESCE(balance.consumed_credits, 0) AS consumed_credits,
                                  CASE
                                      WHEN COALESCE(balance.available_credits, 0) + COALESCE(balance.consumed_credits, 0) > 0
                                      THEN ROUND(
                                          COALESCE(balance.available_credits, 0)::numeric /
                                          (COALESCE(balance.available_credits, 0) + COALESCE(balance.consumed_credits, 0))::numeric,
                                          4)
                                      ELSE 0
                                  END AS remaining_ratio,
                                  CASE
                                      WHEN COALESCE(balance.available_credits, 0) + COALESCE(balance.consumed_credits, 0) > 0
                                      THEN ROUND(
                                          (COALESCE(balance.available_credits, 0)::numeric /
                                          (COALESCE(balance.available_credits, 0) + COALESCE(balance.consumed_credits, 0))::numeric) * 100,
                                          1)
                                      ELSE 0
                                  END AS remaining_percent,
                                  current_subscriptions.trial_ends_at_utc,
                                  usage_rollup.last_usage_at_utc,
                                  COALESCE(usage_rollup.credits_charged, 0) AS credits_charged,
                                  COALESCE(usage_rollup.total_tokens, 0) AS total_tokens,
                                  COALESCE(usage_rollup.estimated_provider_cost, 0) AS estimated_provider_cost
                           FROM workspaces AS workspace
                           LEFT JOIN workspace_credit_balances AS balance ON balance.workspace_id = workspace.id
                           LEFT JOIN current_subscriptions ON current_subscriptions.workspace_id = workspace.id
                           LEFT JOIN member_counts ON member_counts.workspace_id = workspace.id
                           LEFT JOIN usage_rollup ON usage_rollup.workspace_id = workspace.id
                           WHERE workspace.is_active = TRUE
                           ORDER BY workspace.name ASC;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);

        var workspaces = new List<PlatformAdminWorkspaceSummaryDto>();
        while (await reader.ReadAsync(cancellationToken))
        {
            var availableCredits = reader.GetInt64(6);
            var consumedCredits = reader.GetInt64(7);
            var totalTrackedCredits = availableCredits + consumedCredits;
            var estimatedProviderCost = reader.GetDecimal(14);
            var creditsCharged = reader.GetInt64(12);
            var estimatedRevenueBasis = ToRevenueBasis(creditsCharged, creditsPerCurrencyUnit);

            workspaces.Add(new PlatformAdminWorkspaceSummaryDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetInt32(5),
                availableCredits,
                consumedCredits,
                totalTrackedCredits,
                reader.GetDecimal(8),
                reader.GetDecimal(9),
                reader.IsDBNull(10) ? null : reader.GetFieldValue<DateTime>(10),
                reader.IsDBNull(11) ? null : reader.GetFieldValue<DateTime>(11),
                creditsCharged,
                reader.GetInt64(13),
                RoundMoney(estimatedProviderCost),
                estimatedRevenueBasis,
                RoundMoney(estimatedRevenueBasis - estimatedProviderCost)));
        }

        return workspaces;
    }

    public async Task<IReadOnlyList<PlatformAdminModelUsageDto>> ListModelBreakdownAsync(
        int take,
        decimal creditsPerCurrencyUnit,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT usage_event.provider,
                                  usage_event.model,
                                  COUNT(DISTINCT usage_event.workspace_id)::int AS workspace_count,
                                  COUNT(*)::int AS event_count,
                                  COALESCE(SUM(usage_event.credits_charged), 0) AS credits_charged,
                                  COALESCE(SUM(usage_event.total_tokens), 0) AS total_tokens,
                                  COALESCE(SUM(usage_event.estimated_cost), 0) AS estimated_provider_cost,
                                  MAX(usage_event.created_at_utc) AS last_used_at_utc
                           FROM workspace_usage_events AS usage_event
                           GROUP BY usage_event.provider, usage_event.model
                           ORDER BY SUM(usage_event.estimated_cost) DESC,
                                    SUM(usage_event.total_tokens) DESC,
                                    usage_event.provider,
                                    usage_event.model
                           LIMIT @take;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("take", Math.Max(1, take));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var breakdown = new List<PlatformAdminModelUsageDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            var estimatedProviderCost = reader.GetDecimal(6);
            var estimatedRevenueBasis = ToRevenueBasis(reader.GetInt64(4), creditsPerCurrencyUnit);

            breakdown.Add(new PlatformAdminModelUsageDto(
                reader.GetString(0),
                reader.GetString(1),
                reader.GetInt32(2),
                reader.GetInt32(3),
                reader.GetInt64(4),
                reader.GetInt64(5),
                RoundMoney(estimatedProviderCost),
                estimatedRevenueBasis,
                RoundMoney(estimatedRevenueBasis - estimatedProviderCost),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTime>(7)));
        }

        return breakdown;
    }

    public async Task<IReadOnlyList<PlatformAdminBillingTransactionDto>> ListRecentCreditPurchasesAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT transaction.id,
                                  transaction.workspace_id,
                                  workspace.name,
                                  workspace.slug,
                                  transaction.type,
                                  transaction.status,
                                  billing_plan.name,
                                  transaction.credit_amount,
                                  transaction.amount,
                                  transaction.currency_code,
                                  COALESCE(transaction.completed_at_utc, transaction.updated_at_utc, transaction.created_at_utc) AS occurred_at_utc
                           FROM workspace_billing_transactions AS transaction
                           INNER JOIN workspaces AS workspace ON workspace.id = transaction.workspace_id
                           LEFT JOIN billing_plans AS billing_plan ON billing_plan.id = transaction.billing_plan_id
                           WHERE transaction.type = 'CreditPurchase'
                           ORDER BY COALESCE(transaction.completed_at_utc, transaction.updated_at_utc, transaction.created_at_utc) DESC
                           LIMIT @take;
                           """;

        return await ListBillingTransactionsAsync(sql, take, cancellationToken);
    }

    public async Task<IReadOnlyList<PlatformAdminSubscriptionActivityDto>> ListRecentSubscriptionChangesAsync(
        int take,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT subscription.workspace_id,
                                  workspace.name,
                                  workspace.slug,
                                  billing_plan.name,
                                  subscription.status,
                                  subscription.updated_at_utc,
                                  subscription.current_period_end_utc,
                                  subscription.trial_ends_at_utc
                           FROM workspace_subscriptions AS subscription
                           INNER JOIN workspaces AS workspace ON workspace.id = subscription.workspace_id
                           LEFT JOIN billing_plans AS billing_plan ON billing_plan.id = subscription.billing_plan_id
                           ORDER BY subscription.updated_at_utc DESC
                           LIMIT @take;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("take", Math.Max(1, take));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var changes = new List<PlatformAdminSubscriptionActivityDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            changes.Add(new PlatformAdminSubscriptionActivityDto(
                reader.GetGuid(0),
                reader.GetString(1),
                reader.GetString(2),
                reader.IsDBNull(3) ? null : reader.GetString(3),
                reader.GetString(4),
                reader.GetFieldValue<DateTime>(5),
                reader.GetFieldValue<DateTime>(6),
                reader.IsDBNull(7) ? null : reader.GetFieldValue<DateTime>(7)));
        }

        return changes;
    }

    public async Task<IReadOnlyList<PlatformAdminPaymentIssueDto>> ListPaymentIssuesAsync(
        int take,
        int pendingThresholdMinutes,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT transaction.id,
                                  transaction.workspace_id,
                                  workspace.name,
                                  workspace.slug,
                                  transaction.type,
                                  transaction.status,
                                  transaction.amount,
                                  transaction.currency_code,
                                  transaction.created_at_utc,
                                  transaction.updated_at_utc
                           FROM workspace_billing_transactions AS transaction
                           INNER JOIN workspaces AS workspace ON workspace.id = transaction.workspace_id
                           WHERE transaction.status = 'Failed'
                              OR (
                                   transaction.status = 'Pending'
                                   AND transaction.created_at_utc <= NOW() - (@pendingThresholdMinutes * INTERVAL '1 minute')
                                 )
                           ORDER BY transaction.updated_at_utc DESC, transaction.created_at_utc DESC
                           LIMIT @take;
                           """;

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("take", Math.Max(1, take));
        command.Parameters.AddWithValue("pendingThresholdMinutes", Math.Max(1, pendingThresholdMinutes));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var issues = new List<PlatformAdminPaymentIssueDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            issues.Add(new PlatformAdminPaymentIssueDto(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.GetDecimal(6),
                reader.GetString(7),
                reader.GetFieldValue<DateTime>(8),
                reader.GetFieldValue<DateTime>(9)));
        }

        return issues;
    }

    private async Task<IReadOnlyList<PlatformAdminBillingTransactionDto>> ListBillingTransactionsAsync(
        string sql,
        int take,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        command.Parameters.AddWithValue("take", Math.Max(1, take));

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        var transactions = new List<PlatformAdminBillingTransactionDto>();

        while (await reader.ReadAsync(cancellationToken))
        {
            transactions.Add(new PlatformAdminBillingTransactionDto(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetString(2),
                reader.GetString(3),
                reader.GetString(4),
                reader.GetString(5),
                reader.IsDBNull(6) ? null : reader.GetString(6),
                reader.IsDBNull(7) ? null : reader.GetInt64(7),
                reader.GetDecimal(8),
                reader.GetString(9),
                reader.GetFieldValue<DateTime>(10)));
        }

        return transactions;
    }

    private static decimal ToRevenueBasis(long creditsCharged, decimal creditsPerCurrencyUnit)
    {
        if (creditsCharged <= 0 || creditsPerCurrencyUnit <= 0m)
        {
            return 0m;
        }

        return RoundMoney(creditsCharged / creditsPerCurrencyUnit);
    }

    private static decimal RoundMoney(decimal value)
    {
        return decimal.Round(value, 2, MidpointRounding.AwayFromZero);
    }
}
