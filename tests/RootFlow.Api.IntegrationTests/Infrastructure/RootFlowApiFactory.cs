using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.AI;
using RootFlow.Infrastructure.Persistence;

namespace RootFlow.Api.IntegrationTests.Infrastructure;

public sealed class RootFlowApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private const string AdminConnectionString = "Host=localhost;Port=5432;Database=postgres;Username=postgres;Password=postgres";
    private const string TestConnectionString = "Host=localhost;Port=5432;Database=rootflow_test;Username=postgres;Password=postgres";
    private const string WorkspaceId = "11111111-1111-1111-1111-111111111111";

    private readonly string _storageRootPath = Path.Combine(
        Path.GetTempPath(),
        "rootflow-integration-tests",
        Guid.NewGuid().ToString("N"));

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
            services.RemoveAll<NpgsqlDataSource>();

            services.PostConfigure<StorageOptions>(options =>
            {
                options.RootPath = _storageRootPath;
            });

            services.PostConfigure<RootFlowOptions>(options =>
            {
                options.DefaultWorkspaceId = Guid.Parse(WorkspaceId);
                options.DefaultWorkspaceName = "RootFlow Integration Tests";
                options.DefaultWorkspaceSlug = "integration-tests";
            });

            services.AddSingleton(_ =>
            {
                var dataSourceBuilder = new NpgsqlDataSourceBuilder(TestConnectionString);
                dataSourceBuilder.UseVector();
                return dataSourceBuilder.Build();
            });

            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();
            services.AddSingleton<IChatCompletionService, PremiumFakeChatCompletionService>();
        });
        builder.ConfigureAppConfiguration((_, configBuilder) =>
        {
            configBuilder.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = TestConnectionString,
                ["AI:Mode"] = "Fake",
                ["Storage:RootPath"] = _storageRootPath,
                ["RootFlow:DefaultWorkspaceId"] = WorkspaceId,
                ["RootFlow:DefaultWorkspaceName"] = "RootFlow Integration Tests",
                ["RootFlow:DefaultWorkspaceSlug"] = "integration-tests"
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

    public async Task InitializeAsync()
    {
        Directory.CreateDirectory(_storageRootPath);

        await EnsureDatabaseExistsAsync();
        await ResetStateAsync();
    }

    public new async Task DisposeAsync()
    {
        await base.DisposeAsync();

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
        await InitializeDatabaseAsync();
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
                                       conversation_messages,
                                       conversations,
                                       document_chunks,
                                       knowledge_documents,
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
        var options = Options.Create(new RootFlowOptions
        {
            DefaultWorkspaceId = Guid.Parse(WorkspaceId),
            DefaultWorkspaceName = "RootFlow Integration Tests",
            DefaultWorkspaceSlug = "integration-tests"
        });

        var builder = new NpgsqlDataSourceBuilder(TestConnectionString);
        builder.UseVector();

        await using var dataSource = builder.Build();
        var initializer = new PostgresDatabaseInitializer(dataSource, options);
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
