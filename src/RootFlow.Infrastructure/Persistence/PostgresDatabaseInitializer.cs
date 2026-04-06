using Microsoft.Extensions.Logging;
using Npgsql;

namespace RootFlow.Infrastructure.Persistence;

public sealed class PostgresDatabaseInitializer
{
    private readonly NpgsqlDataSource _dataSource;
    private readonly ILogger<PostgresDatabaseInitializer> _logger;

    public PostgresDatabaseInitializer(
        NpgsqlDataSource dataSource,
        ILogger<PostgresDatabaseInitializer> logger)
    {
        _dataSource = dataSource;
        _logger = logger;
    }

    public async Task InitializeAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Checking PostgreSQL schema migrations.");
        await using var connection = await _dataSource.OpenConnectionAsync(cancellationToken);
        await EnsureMigrationsTableAsync(connection, cancellationToken);

        var appliedMigrationIds = await LoadAppliedMigrationIdsAsync(connection, cancellationToken);
        foreach (var migration in GetMigrations())
        {
            if (appliedMigrationIds.Contains(migration.Id))
            {
                continue;
            }

            _logger.LogInformation("Applying database migration {MigrationId}: {MigrationName}", migration.Id, migration.Name);
            await using var transaction = await connection.BeginTransactionAsync(cancellationToken);

            await using (var migrationCommand = new NpgsqlCommand(migration.Sql, connection, transaction))
            {
                await migrationCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using (var insertCommand = new NpgsqlCommand(
                             """
                             INSERT INTO schema_migrations (id, name, applied_at_utc)
                             VALUES (@id, @name, @appliedAtUtc);
                             """,
                             connection,
                             transaction))
            {
                insertCommand.Parameters.AddWithValue("id", migration.Id);
                insertCommand.Parameters.AddWithValue("name", migration.Name);
                insertCommand.Parameters.AddWithValue("appliedAtUtc", DateTime.UtcNow);

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await transaction.CommitAsync(cancellationToken);
            _logger.LogInformation("Applied database migration {MigrationId}", migration.Id);
        }

        _logger.LogInformation("PostgreSQL schema migrations are up to date.");
    }

    private static async Task EnsureMigrationsTableAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           CREATE TABLE IF NOT EXISTS schema_migrations (
                               id text PRIMARY KEY,
                               name text NOT NULL,
                               applied_at_utc timestamptz NOT NULL
                           );
                           """;

        await using var command = new NpgsqlCommand(sql, connection);
        await command.ExecuteNonQueryAsync(cancellationToken);
    }

    private static async Task<HashSet<string>> LoadAppliedMigrationIdsAsync(
        NpgsqlConnection connection,
        CancellationToken cancellationToken)
    {
        const string sql = """
                           SELECT id
                           FROM schema_migrations;
                           """;

        var ids = new HashSet<string>(StringComparer.Ordinal);

        await using var command = new NpgsqlCommand(sql, connection);
        await using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            ids.Add(reader.GetString(0));
        }

        return ids;
    }

    private static IReadOnlyList<DatabaseMigration> GetMigrations()
    {
        return
        [
            new DatabaseMigration(
                "202603280001_base_schema",
                "Create core RootFlow knowledge and conversation schema",
                """
                CREATE EXTENSION IF NOT EXISTS vector;

                CREATE TABLE IF NOT EXISTS workspaces (
                    id uuid PRIMARY KEY,
                    name text NOT NULL,
                    slug text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    is_active boolean NOT NULL
                );

                CREATE TABLE IF NOT EXISTS knowledge_documents (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id),
                    original_file_name text NOT NULL,
                    content_type text NOT NULL,
                    size_bytes bigint NOT NULL,
                    storage_path text NOT NULL,
                    checksum text NOT NULL,
                    extracted_text text NULL,
                    status text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    processed_at_utc timestamptz NULL,
                    failure_reason text NULL
                );

                CREATE TABLE IF NOT EXISTS document_chunks (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id),
                    document_id uuid NOT NULL REFERENCES knowledge_documents(id) ON DELETE CASCADE,
                    sequence integer NOT NULL,
                    content text NOT NULL,
                    embedding vector(1536) NULL,
                    token_count integer NOT NULL,
                    source_label text NOT NULL,
                    created_at_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS conversations (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id),
                    title text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    updated_at_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS conversation_messages (
                    id uuid PRIMARY KEY,
                    conversation_id uuid NOT NULL REFERENCES conversations(id) ON DELETE CASCADE,
                    role text NOT NULL,
                    content text NOT NULL,
                    model_name text NULL,
                    created_at_utc timestamptz NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspaces_slug
                    ON workspaces (slug);

                CREATE INDEX IF NOT EXISTS ix_knowledge_documents_workspace_created
                    ON knowledge_documents (workspace_id, created_at_utc DESC);

                CREATE INDEX IF NOT EXISTS ix_document_chunks_workspace_document_sequence
                    ON document_chunks (workspace_id, document_id, sequence);

                CREATE INDEX IF NOT EXISTS ix_conversations_workspace_updated
                    ON conversations (workspace_id, updated_at_utc DESC);

                CREATE INDEX IF NOT EXISTS ix_conversation_messages_conversation_created
                    ON conversation_messages (conversation_id, created_at_utc);
                """),
            new DatabaseMigration(
                "202603310001_auth_multi_tenant_foundation",
                "Create users and workspace memberships for SaaS tenancy",
                """
                CREATE TABLE IF NOT EXISTS app_users (
                    id uuid PRIMARY KEY,
                    email text NOT NULL,
                    normalized_email text NOT NULL,
                    full_name text NOT NULL,
                    password_hash text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    is_active boolean NOT NULL
                );

                CREATE TABLE IF NOT EXISTS workspace_memberships (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                    role text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    is_active boolean NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_app_users_normalized_email
                    ON app_users (normalized_email);

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_memberships_workspace_user
                    ON workspace_memberships (workspace_id, user_id);

                CREATE INDEX IF NOT EXISTS ix_workspace_memberships_user_created
                    ON workspace_memberships (user_id, created_at_utc);
                """),
            new DatabaseMigration(
                "202604010001_workspace_invitation_foundation",
                "Create workspace invitations for future explicit shared-workspace membership",
                """
                CREATE TABLE IF NOT EXISTS workspace_invitations (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    email text NOT NULL,
                    normalized_email text NOT NULL,
                    role text NOT NULL,
                    token text NOT NULL,
                    invited_by_user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                    status text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    expires_at_utc timestamptz NOT NULL,
                    accepted_at_utc timestamptz NULL,
                    revoked_at_utc timestamptz NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_invitations_token
                    ON workspace_invitations (token);

                CREATE INDEX IF NOT EXISTS ix_workspace_invitations_workspace_email_status
                    ON workspace_invitations (workspace_id, normalized_email, status);

                CREATE INDEX IF NOT EXISTS ix_workspace_invitations_workspace_created
                    ON workspace_invitations (workspace_id, created_at_utc DESC);
                """),
            new DatabaseMigration(
                "202604010002_password_reset_foundation",
                "Create password reset tokens for secure account recovery",
                """
                CREATE TABLE IF NOT EXISTS password_reset_tokens (
                    id uuid PRIMARY KEY,
                    user_id uuid NOT NULL REFERENCES app_users(id) ON DELETE CASCADE,
                    token_hash text NOT NULL,
                    created_at_utc timestamptz NOT NULL,
                    expires_at_utc timestamptz NOT NULL,
                    used_at_utc timestamptz NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_password_reset_tokens_token_hash
                    ON password_reset_tokens (token_hash);

                CREATE INDEX IF NOT EXISTS ix_password_reset_tokens_user_created
                    ON password_reset_tokens (user_id, created_at_utc DESC);
                """),
            new DatabaseMigration(
                "202604020001_workspace_invitation_token_hardening",
                "Hash workspace invitation tokens and add workspace membership listing index",
                """
                CREATE EXTENSION IF NOT EXISTS pgcrypto;

                DO $$
                BEGIN
                    IF EXISTS (
                        SELECT 1
                        FROM information_schema.columns
                        WHERE table_name = 'workspace_invitations'
                          AND column_name = 'token'
                    ) THEN
                        ALTER TABLE workspace_invitations RENAME COLUMN token TO token_hash;
                    END IF;
                END
                $$;

                UPDATE workspace_invitations
                SET token_hash = encode(digest(token_hash, 'sha256'), 'hex')
                WHERE token_hash IS NOT NULL
                  AND char_length(token_hash) <> 64;

                DROP INDEX IF EXISTS ix_workspace_invitations_token;

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_invitations_token_hash
                    ON workspace_invitations (token_hash);

                CREATE INDEX IF NOT EXISTS ix_workspace_memberships_workspace_created
                    ON workspace_memberships (workspace_id, created_at_utc, id);
                """),
            new DatabaseMigration(
                "202604040001_workspace_billing_foundation",
                "Create workspace billing plans, subscriptions, shared credits, and AI usage tracking",
                """
                CREATE EXTENSION IF NOT EXISTS pgcrypto;

                CREATE TABLE IF NOT EXISTS billing_plans (
                    id uuid PRIMARY KEY,
                    code text NOT NULL,
                    name text NOT NULL,
                    monthly_price numeric(18,2) NOT NULL,
                    currency_code text NOT NULL,
                    included_credits bigint NOT NULL,
                    max_users integer NOT NULL,
                    is_active boolean NOT NULL,
                    created_at_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS workspace_subscriptions (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    billing_plan_id uuid NOT NULL REFERENCES billing_plans(id),
                    status text NOT NULL,
                    current_period_start_utc timestamptz NOT NULL,
                    current_period_end_utc timestamptz NOT NULL,
                    canceled_at_utc timestamptz NULL,
                    created_at_utc timestamptz NOT NULL,
                    updated_at_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS workspace_credit_balances (
                    workspace_id uuid PRIMARY KEY REFERENCES workspaces(id) ON DELETE CASCADE,
                    available_credits bigint NOT NULL,
                    consumed_credits bigint NOT NULL,
                    updated_at_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS workspace_credit_ledger (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    type text NOT NULL,
                    amount bigint NOT NULL,
                    description text NOT NULL,
                    reference_type text NULL,
                    reference_id text NULL,
                    created_at_utc timestamptz NOT NULL
                );

                CREATE TABLE IF NOT EXISTS workspace_usage_events (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    user_id uuid NULL REFERENCES app_users(id) ON DELETE SET NULL,
                    conversation_id uuid NULL REFERENCES conversations(id) ON DELETE SET NULL,
                    provider text NOT NULL,
                    model text NOT NULL,
                    prompt_tokens integer NOT NULL,
                    completion_tokens integer NOT NULL,
                    total_tokens integer NOT NULL,
                    estimated_cost numeric(18,6) NOT NULL,
                    credits_charged bigint NOT NULL,
                    created_at_utc timestamptz NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_billing_plans_code
                    ON billing_plans (code);

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_subscriptions_workspace_active
                    ON workspace_subscriptions (workspace_id)
                    WHERE status = 'Active';

                CREATE INDEX IF NOT EXISTS ix_workspace_subscriptions_workspace_period_end
                    ON workspace_subscriptions (workspace_id, current_period_end_utc DESC, created_at_utc DESC);

                CREATE INDEX IF NOT EXISTS ix_workspace_credit_ledger_workspace_created
                    ON workspace_credit_ledger (workspace_id, created_at_utc DESC, id DESC);

                CREATE INDEX IF NOT EXISTS ix_workspace_usage_events_workspace_created
                    ON workspace_usage_events (workspace_id, created_at_utc DESC, id DESC);

                CREATE INDEX IF NOT EXISTS ix_workspace_usage_events_conversation_created
                    ON workspace_usage_events (conversation_id, created_at_utc DESC, id DESC);

                INSERT INTO billing_plans (
                    id,
                    code,
                    name,
                    monthly_price,
                    currency_code,
                    included_credits,
                    max_users,
                    is_active,
                    created_at_utc
                )
                VALUES
                    ('7d402ac6-b828-4df0-92de-d7d4bf79e001', 'starter', 'Starter', 49.90, 'BRL', 10000, 3, TRUE, NOW()),
                    ('7d402ac6-b828-4df0-92de-d7d4bf79e002', 'pro', 'Pro', 99.90, 'BRL', 25000, 10, TRUE, NOW()),
                    ('7d402ac6-b828-4df0-92de-d7d4bf79e003', 'business', 'Business', 199.90, 'BRL', 50000, 50, TRUE, NOW())
                ON CONFLICT (code) DO UPDATE
                SET name = EXCLUDED.name,
                    monthly_price = EXCLUDED.monthly_price,
                    currency_code = EXCLUDED.currency_code,
                    included_credits = EXCLUDED.included_credits,
                    max_users = EXCLUDED.max_users,
                    is_active = EXCLUDED.is_active;

                CREATE TEMP TABLE temp_seeded_workspace_subscriptions (
                    id uuid NOT NULL,
                    workspace_id uuid NOT NULL
                )
                ON COMMIT DROP;

                WITH starter_plan AS (
                    SELECT id
                    FROM billing_plans
                    WHERE code = 'starter'
                ),
                inserted_subscriptions AS (
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
                    SELECT gen_random_uuid(),
                           w.id,
                           starter_plan.id,
                           'Active',
                           NOW(),
                           NOW() + INTERVAL '1 month',
                           NULL,
                           NOW(),
                           NOW()
                    FROM workspaces AS w
                    CROSS JOIN starter_plan
                    WHERE NOT EXISTS (
                        SELECT 1
                        FROM workspace_subscriptions AS s
                        WHERE s.workspace_id = w.id
                    )
                    RETURNING id, workspace_id
                )
                INSERT INTO temp_seeded_workspace_subscriptions (id, workspace_id)
                SELECT id, workspace_id
                FROM inserted_subscriptions;

                INSERT INTO workspace_credit_balances (
                    workspace_id,
                    available_credits,
                    consumed_credits,
                    updated_at_utc
                )
                SELECT w.id, 0, 0, NOW()
                FROM workspaces AS w
                WHERE NOT EXISTS (
                    SELECT 1
                    FROM workspace_credit_balances AS b
                    WHERE b.workspace_id = w.id
                );

                UPDATE workspace_credit_balances AS b
                SET available_credits = b.available_credits + starter_plan.included_credits,
                    updated_at_utc = NOW()
                FROM billing_plans AS starter_plan
                CROSS JOIN temp_seeded_workspace_subscriptions AS seeded
                WHERE starter_plan.code = 'starter'
                  AND seeded.workspace_id = b.workspace_id;

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
                SELECT gen_random_uuid(),
                       seeded.workspace_id,
                       'SubscriptionGrant',
                       starter_plan.included_credits,
                       'Included credits for the Starter plan',
                       'workspace_subscription',
                       seeded.id::text,
                       NOW()
                FROM temp_seeded_workspace_subscriptions AS seeded
                CROSS JOIN billing_plans AS starter_plan
                WHERE starter_plan.code = 'starter';
                """),
            new DatabaseMigration(
                "202604040002_workspace_billing_trial_support",
                "Add trial expiry metadata and convert seeded starter subscriptions into trials",
                """
                ALTER TABLE workspace_subscriptions
                ADD COLUMN IF NOT EXISTS trial_ends_at_utc timestamptz NULL;

                UPDATE workspace_subscriptions
                SET trial_ends_at_utc = current_period_end_utc
                WHERE status = 'Trial'
                  AND trial_ends_at_utc IS NULL;

                UPDATE workspace_subscriptions AS subscription
                SET status = 'Trial',
                    current_period_end_utc = LEAST(
                        subscription.current_period_end_utc,
                        subscription.current_period_start_utc + INTERVAL '7 days'
                    ),
                    trial_ends_at_utc = LEAST(
                        subscription.current_period_end_utc,
                        subscription.current_period_start_utc + INTERVAL '7 days'
                    ),
                    updated_at_utc = NOW()
                FROM billing_plans AS plan
                WHERE subscription.billing_plan_id = plan.id
                  AND plan.code = 'starter'
                  AND subscription.status = 'Active'
                  AND subscription.canceled_at_utc IS NULL
                  AND subscription.trial_ends_at_utc IS NULL;

                DROP INDEX IF EXISTS ix_workspace_subscriptions_workspace_active;

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_subscriptions_workspace_current
                    ON workspace_subscriptions (workspace_id)
                    WHERE status IN ('Active', 'Trial');
                """),
            new DatabaseMigration(
                "202604040003_workspace_billing_stripe_payments",
                "Add Stripe payment transaction auditability and external subscription references",
                """
                ALTER TABLE workspace_subscriptions
                ADD COLUMN IF NOT EXISTS provider text NULL;

                ALTER TABLE workspace_subscriptions
                ADD COLUMN IF NOT EXISTS provider_customer_id text NULL;

                ALTER TABLE workspace_subscriptions
                ADD COLUMN IF NOT EXISTS provider_subscription_id text NULL;

                ALTER TABLE workspace_subscriptions
                ADD COLUMN IF NOT EXISTS provider_price_id text NULL;

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_subscriptions_provider_subscription
                    ON workspace_subscriptions (provider, provider_subscription_id)
                    WHERE provider IS NOT NULL
                      AND provider_subscription_id IS NOT NULL;

                CREATE TABLE IF NOT EXISTS workspace_billing_transactions (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NOT NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    provider text NOT NULL,
                    type text NOT NULL,
                    status text NOT NULL,
                    billing_plan_id uuid NULL REFERENCES billing_plans(id),
                    credit_amount bigint NULL,
                    amount numeric(18,2) NOT NULL,
                    currency_code text NOT NULL,
                    external_checkout_session_id text NULL,
                    external_payment_intent_id text NULL,
                    external_subscription_id text NULL,
                    external_invoice_id text NULL,
                    external_customer_id text NULL,
                    created_at_utc timestamptz NOT NULL,
                    updated_at_utc timestamptz NOT NULL,
                    completed_at_utc timestamptz NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_credit_ledger_reference_unique
                    ON workspace_credit_ledger (reference_type, reference_id)
                    WHERE reference_type IS NOT NULL
                      AND reference_id IS NOT NULL;

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_billing_transactions_checkout
                    ON workspace_billing_transactions (provider, external_checkout_session_id)
                    WHERE external_checkout_session_id IS NOT NULL;

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_billing_transactions_invoice
                    ON workspace_billing_transactions (provider, external_invoice_id)
                    WHERE external_invoice_id IS NOT NULL;

                CREATE INDEX IF NOT EXISTS ix_workspace_billing_transactions_workspace_created
                    ON workspace_billing_transactions (workspace_id, created_at_utc DESC, id DESC);

                CREATE INDEX IF NOT EXISTS ix_workspace_billing_transactions_subscription_updated
                    ON workspace_billing_transactions (provider, external_subscription_id, updated_at_utc DESC, id DESC)
                    WHERE external_subscription_id IS NOT NULL;

                UPDATE billing_plans
                SET monthly_price = CASE code
                        WHEN 'starter' THEN 49.90
                        WHEN 'pro' THEN 99.90
                        WHEN 'business' THEN 199.90
                        ELSE monthly_price
                    END,
                    currency_code = 'BRL',
                    is_active = TRUE
                WHERE code IN ('starter', 'pro', 'business');
                """),
            new DatabaseMigration(
                "202604050001_workspace_billing_stripe_lookup_indexes",
                "Add Stripe customer lookup indexes for subscription synchronization",
                """
                CREATE INDEX IF NOT EXISTS ix_workspace_subscriptions_provider_customer
                    ON workspace_subscriptions (provider, provider_customer_id, updated_at_utc DESC, id DESC)
                    WHERE provider IS NOT NULL
                      AND provider_customer_id IS NOT NULL;

                CREATE INDEX IF NOT EXISTS ix_workspace_billing_transactions_customer_pending
                    ON workspace_billing_transactions (provider, external_customer_id, updated_at_utc DESC, id DESC)
                    WHERE external_customer_id IS NOT NULL
                      AND status = 'Pending';
                """),
            new DatabaseMigration(
                "202604050002_workspace_billing_webhook_events",
                "Persist Stripe webhook deliveries for durable replay and diagnostics",
                """
                CREATE TABLE IF NOT EXISTS workspace_billing_webhook_events (
                    id uuid PRIMARY KEY,
                    provider text NOT NULL,
                    provider_event_id text NOT NULL,
                    event_type text NOT NULL,
                    status text NOT NULL,
                    attempt_count integer NOT NULL,
                    payload text NOT NULL,
                    signature_header text NOT NULL,
                    first_received_at_utc timestamptz NOT NULL,
                    last_received_at_utc timestamptz NOT NULL,
                    updated_at_utc timestamptz NOT NULL,
                    processing_started_at_utc timestamptz NULL,
                    processed_at_utc timestamptz NULL,
                    last_error text NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_billing_webhook_events_provider_event
                    ON workspace_billing_webhook_events (provider, provider_event_id);

                CREATE INDEX IF NOT EXISTS ix_workspace_billing_webhook_events_provider_status_updated
                    ON workspace_billing_webhook_events (provider, status, updated_at_utc ASC, id ASC);

                CREATE INDEX IF NOT EXISTS ix_workspace_billing_webhook_events_provider_processing
                    ON workspace_billing_webhook_events (provider, processing_started_at_utc ASC, id ASC)
                    WHERE status = 'Processing';
                """),
            new DatabaseMigration(
                "202604060001_workspace_billing_notification_deliveries",
                "Persist sent billing notification deliveries for dedupe and lifecycle automation",
                """
                CREATE TABLE IF NOT EXISTS workspace_billing_notification_deliveries (
                    id uuid PRIMARY KEY,
                    workspace_id uuid NULL REFERENCES workspaces(id) ON DELETE CASCADE,
                    notification_kind text NOT NULL,
                    dedupe_key text NOT NULL,
                    recipient_email text NOT NULL,
                    sent_at_utc timestamptz NOT NULL
                );

                CREATE UNIQUE INDEX IF NOT EXISTS ix_workspace_billing_notification_deliveries_unique
                    ON workspace_billing_notification_deliveries (notification_kind, dedupe_key, recipient_email);

                CREATE INDEX IF NOT EXISTS ix_workspace_billing_notification_deliveries_workspace_sent
                    ON workspace_billing_notification_deliveries (workspace_id, sent_at_utc DESC, id DESC);
                """),
            new DatabaseMigration(
                "202604060002_workspace_billing_plan_repricing",
                "Align billing plans and bundled credits with the BRL go-to-market pricing ladder",
                """
                UPDATE billing_plans
                SET monthly_price = CASE code
                        WHEN 'starter' THEN 49.90
                        WHEN 'pro' THEN 99.90
                        WHEN 'business' THEN 199.90
                        ELSE monthly_price
                    END,
                    currency_code = 'BRL',
                    included_credits = CASE code
                        WHEN 'starter' THEN 10000
                        WHEN 'pro' THEN 25000
                        WHEN 'business' THEN 50000
                        ELSE included_credits
                    END,
                    max_users = CASE code
                        WHEN 'starter' THEN 3
                        WHEN 'pro' THEN 10
                        WHEN 'business' THEN 50
                        ELSE max_users
                    END,
                    is_active = TRUE
                WHERE code IN ('starter', 'pro', 'business');
                """)
        ];
    }

    private sealed record DatabaseMigration(string Id, string Name, string Sql);
}
