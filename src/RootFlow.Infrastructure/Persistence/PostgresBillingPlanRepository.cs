using Npgsql;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Domain.Billing;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresBillingPlanRepository : IBillingPlanRepository
{
    private readonly NpgsqlDataSource _dataSource;

    public PostgresBillingPlanRepository(NpgsqlDataSource dataSource)
    {
        _dataSource = dataSource;
    }

    public Task<BillingPlan?> GetByCodeAsync(
        string code,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  code,
                                  name,
                                  monthly_price,
                                  currency_code,
                                  included_credits,
                                  max_users,
                                  is_active,
                                  created_at_utc
                           FROM billing_plans
                           WHERE code = @code
                           LIMIT 1;
                           """;

        return GetSingleAsync(
            sql,
            static (command, state) => command.Parameters.AddWithValue("code", state.Trim().ToLowerInvariant()),
            code,
            cancellationToken);
    }

    public Task<BillingPlan?> GetByIdAsync(
        Guid billingPlanId,
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  code,
                                  name,
                                  monthly_price,
                                  currency_code,
                                  included_credits,
                                  max_users,
                                  is_active,
                                  created_at_utc
                           FROM billing_plans
                           WHERE id = @billingPlanId
                           LIMIT 1;
                           """;

        return GetSingleAsync(
            sql,
            static (command, state) => command.Parameters.AddWithValue("billingPlanId", state),
            billingPlanId,
            cancellationToken);
    }

    public async Task<IReadOnlyList<BillingPlan>> ListActiveAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  code,
                                  name,
                                  monthly_price,
                                  currency_code,
                                  included_credits,
                                  max_users,
                                  is_active,
                                  created_at_utc
                           FROM billing_plans
                           WHERE is_active = TRUE
                           ORDER BY monthly_price, name, id;
                           """;

        return await ListAsync(sql, cancellationToken);
    }

    public async Task<IReadOnlyList<BillingPlan>> ListAllAsync(
        CancellationToken cancellationToken = default)
    {
        const string sql = """
                           SELECT id,
                                  code,
                                  name,
                                  monthly_price,
                                  currency_code,
                                  included_credits,
                                  max_users,
                                  is_active,
                                  created_at_utc
                           FROM billing_plans
                           ORDER BY monthly_price, name, id;
                           """;

        return await ListAsync(sql, cancellationToken);
    }

    private async Task<IReadOnlyList<BillingPlan>> ListAsync(
        string sql,
        CancellationToken cancellationToken)
    {
        var plans = new List<BillingPlan>();

        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            plans.Add(MapPlan(reader));
        }

        return plans;
    }

    private async Task<BillingPlan?> GetSingleAsync<TState>(
        string sql,
        Action<NpgsqlCommand, TState> configureParameters,
        TState state,
        CancellationToken cancellationToken)
    {
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await using var command = new NpgsqlCommand(sql, connection);
        configureParameters(command, state);

        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        return await reader.ReadAsync(cancellationToken)
            ? MapPlan(reader)
            : null;
    }

    private static BillingPlan MapPlan(NpgsqlDataReader reader)
    {
        var plan = new BillingPlan(
            reader.GetGuid(0),
            reader.GetString(1),
            reader.GetString(2),
            reader.GetFieldValue<decimal>(3),
            reader.GetString(4),
            reader.GetInt64(5),
            reader.GetInt32(6),
            reader.GetFieldValue<DateTime>(8));

        if (!reader.GetBoolean(7))
        {
            plan.Deactivate();
        }

        return plan;
    }
}
