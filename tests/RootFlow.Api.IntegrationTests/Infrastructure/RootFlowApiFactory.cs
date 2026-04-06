using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Api.Contracts.Auth;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.AI;
using RootFlow.Infrastructure.Persistence;

namespace RootFlow.Api.IntegrationTests.Infrastructure;

public sealed class RootFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AdminConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
    private const string TestConnectionString = "Host=localhost;Port=5432;Database=rootflow_test;Username=postgres;Password=postgres";
    private const string TestJwtKey = "rootflow-integration-jwt-key-2026-03-31";

    private readonly string _storageRootPath = Path.Combine(
        Path.GetTempPath(),
        "rootflow-integration-tests",
        Guid.NewGuid().ToString("N"));
    private readonly string? _originalRootFlowDatabaseUrl = Environment.GetEnvironmentVariable("ROOTFLOW_DATABASE_URL");
    private readonly string? _originalJwtKey = Environment.GetEnvironmentVariable("ROOTFLOW_JWT_KEY");

    public RootFlowApiFactory()
    {
        Environment.SetEnvironmentVariable("ROOTFLOW_DATABASE_URL", TestConnectionString);
        Environment.SetEnvironmentVariable("ROOTFLOW_JWT_KEY", TestJwtKey);
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("IntegrationTesting");
        builder.ConfigureLogging(logging =>
        {
            logging.ClearProviders();
        });
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IEmbeddingService>();
            services.RemoveAll<IChatCompletionService>();
            services.RemoveAll<IPasswordResetNotifier>();
            services.RemoveAll<IWorkspaceInvitationNotifier>();
            services.RemoveAll<IWorkspaceBillingNotifier>();
            services.RemoveAll<NpgsqlDataSource>();

            services.PostConfigure<StorageOptions>(options =>
            {
                options.RootPath = _storageRootPath;
            });

            services.AddSingleton(_ =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(TestConnectionString);
                dataSourceBuilder.UseVector();
                return dataSourceBuilder.Build();
            });

            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();
            services.AddSingleton<IChatCompletionService, PremiumFakeChatCompletionService>();
            services.AddSingleton<TestPasswordResetNotifier>();
            services.AddSingleton<IPasswordResetNotifier>(serviceProvider =>
                serviceProvider.GetRequiredService<TestPasswordResetNotifier>());
            services.AddSingleton<TestWorkspaceInvitationNotifier>();
            services.AddSingleton<IWorkspaceInvitationNotifier>(serviceProvider =>
                serviceProvider.GetRequiredService<TestWorkspaceInvitationNotifier>());
            services.AddSingleton<TestWorkspaceBillingNotifier>();
            services.AddSingleton<IWorkspaceBillingNotifier>(serviceProvider =>
                serviceProvider.GetRequiredService<TestWorkspaceBillingNotifier>());
        });
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = TestConnectionString,
                ["AI:Mode"] = "Fake",
                ["Storage:RootPath"] = _storageRootPath,
                ["Jwt:Issuer"] = "RootFlow.Tests",
                ["Jwt:Audience"] = "RootFlow.Web.Tests",
                ["Jwt:Key"] = TestJwtKey,
                ["PlatformAdmin:Emails:0"] = "admin@rootflow.test"
            });
        });
    }

    public HttpClient CreateApiClient()
    {
        return CreateClient(new WebApplicationFactoryClientOptions
        {
            BaseAddress = new Uri("https://localhost")
        });
    }

    public async Task<HttpClient> CreateAuthenticatedClientAsync(
        string? fullName = null,
        string? email = null,
        string password = "Password123!",
        string? workspaceName = null)
    {
        var uniqueSuffix = Guid.NewGuid().ToString("N")[..8];
        var client = CreateApiClient();
        var signupResponse = await client.PostAsJsonAsync("/api/auth/signup", new
        {
            fullName = fullName ?? $"RootFlow Tester {uniqueSuffix}",
            email = email ?? $"tester-{uniqueSuffix}@rootflow.test",
            password,
            workspaceName = workspaceName ?? $"Workspace {uniqueSuffix}"
        });

        signupResponse.EnsureSuccessStatusCode();

        var payload = await signupResponse.Content.ReadFromJsonAsync<AuthResponse>();
        if (payload is null)
        {
            throw new Xunit.Sdk.XunitException("Signup did not return an auth payload.");
        }

        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", payload.Token);
        return client;
    }

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_storageRootPath);

        await EnsureDatabaseExistsAsync();
        await ResetStateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

        Environment.SetEnvironmentVariable("ROOTFLOW_DATABASE_URL", _originalRootFlowDatabaseUrl);
        Environment.SetEnvironmentVariable("ROOTFLOW_JWT_KEY", _originalJwtKey);

        if (Directory.Exists(_storageRootPath))
        {
            Directory.Delete(_storageRootPath, recursive: true);
        }
    }

    public async Task ResetStateAsync()
    {
        await InitializeDatabaseAsync();
        await TruncateTablesAsync();
        ClearStorage();
        Services.GetRequiredService<TestPasswordResetNotifier>().Clear();
        Services.GetRequiredService<TestWorkspaceInvitationNotifier>().Clear();
        Services.GetRequiredService<TestWorkspaceBillingNotifier>().Clear();
        await InitializeDatabaseAsync();
    }

    public PasswordResetNotification? GetLatestPasswordResetNotification(string email)
    {
        return Services.GetRequiredService<TestPasswordResetNotifier>().GetLatestForEmail(email);
    }

    public WorkspaceInvitationNotification? GetLatestWorkspaceInvitationNotification(string email)
    {
        return Services.GetRequiredService<TestWorkspaceInvitationNotifier>().GetLatestForEmail(email);
    }

    public TestWorkspaceBillingNotifier GetWorkspaceBillingNotifier()
    {
        return Services.GetRequiredService<TestWorkspaceBillingNotifier>();
    }

    private static async Task EnsureDatabaseExistsAsync()
    {
        const string databaseName = "rootflow_test";

        await using var connection = new NpgsqlConnection(AdminConnectionString);
        await connection.OpenAsync();

        await using (var existsCommand = new NpgsqlCommand(
                         "SELECT 1 FROM pg_database WHERE datname = @databaseName;",
                         connection))
        {
            existsCommand.Parameters.AddWithValue("databaseName", databaseName);
            var exists = await existsCommand.ExecuteScalarAsync();
            if (exists is not null)
            {
                return;
            }
        }

        await using var createCommand = new NpgsqlCommand("CREATE DATABASE \"rootflow_test\";", connection);
        await createCommand.ExecuteNonQueryAsync();
    }

    private static async Task TruncateTablesAsync()
    {
        const string truncateSql = """
                                   TRUNCATE TABLE
                                       workspace_billing_notification_deliveries,
                                       workspace_billing_webhook_events,
                                       workspace_billing_transactions,
                                       workspace_usage_events,
                                       workspace_credit_ledger,
                                       workspace_credit_balances,
                                       workspace_subscriptions,
                                       conversation_messages,
                                       conversations,
                                       document_chunks,
                                       knowledge_documents,
                                       password_reset_tokens,
                                       workspace_invitations,
                                       workspace_memberships,
                                       app_users,
                                       workspaces
                                   CASCADE;
                                   """;

        await using var connection = new NpgsqlConnection(TestConnectionString);
        await connection.OpenAsync();

        await using var command = new NpgsqlCommand(truncateSql, connection);
        await command.ExecuteNonQueryAsync();
    }

    private static async Task InitializeDatabaseAsync()
    {
        var builder = new NpgsqlDataSourceBuilder(TestConnectionString);
        builder.UseVector();

        await using var dataSource = builder.Build();
        var initializer = new PostgresDatabaseInitializer(dataSource, NullLogger<PostgresDatabaseInitializer>.Instance);
        await initializer.InitializeAsync();
    }

    private void ClearStorage()
    {
        if (Directory.Exists(_storageRootPath))
        {
            Directory.Delete(_storageRootPath, recursive: true);
        }

        Directory.CreateDirectory(_storageRootPath);
    }
}
