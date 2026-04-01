using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Auth;
using RootFlow.Application.Chat;
using RootFlow.Application.Conversations;
using RootFlow.Application.Documents;
using RootFlow.Infrastructure.AI;
using RootFlow.Infrastructure.Auth;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.Documents;
using RootFlow.Infrastructure.Persistence;
using RootFlow.Infrastructure.Search;
using RootFlow.Infrastructure.Storage;
using RootFlow.Infrastructure.Time;

namespace RootFlow.Infrastructure.DependencyInjection;

public static class InfrastructureServiceCollectionExtensions
{
    public static IServiceCollection AddRootFlowInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        var connectionString = ResolvePostgresConnectionString(configuration);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                "PostgreSQL connection string is not configured. Set ROOTFLOW_DATABASE_URL, DATABASE_URL, or ConnectionStrings:Postgres.");
        }

        services.Configure<AiOptions>(configuration.GetSection("AI"));
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<TextChunkingOptions>(configuration.GetSection("Chunking"));
        services.PostConfigure<OpenAiOptions>(options =>
        {
            options.ApiKey = ResolveOpenAiApiKey(configuration, options);
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector();
            return builder.Build();
        });

        services.AddSingleton<PostgresDatabaseInitializer>();

        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentTextExtractor, SimpleDocumentTextExtractor>();
        services.AddScoped<ITextChunker, SimpleTextChunker>();

        services.AddScoped<IPasswordHashingService, AspNetPasswordHashingService>();
        services.AddScoped<IAuthRepository, PostgresAuthRepository>();
        services.AddScoped<IWorkspaceInvitationRepository, PostgresWorkspaceInvitationRepository>();
        services.AddScoped<IWorkspaceRepository, PostgresWorkspaceRepository>();
        services.AddScoped<IKnowledgeDocumentRepository, PostgresKnowledgeDocumentRepository>();
        services.AddScoped<IDocumentChunkRepository, PostgresDocumentChunkRepository>();
        services.AddScoped<IConversationRepository, PostgresConversationRepository>();
        services.AddScoped<IKnowledgeSearchService, PostgresKnowledgeSearchService>();
        services.AddScoped<IUnitOfWork, ImmediateUnitOfWork>();

        var aiMode = configuration.GetValue<string>("AI:Mode") ?? "OpenAI";
        if (string.Equals(aiMode, "Fake", StringComparison.OrdinalIgnoreCase))
        {
            services.AddSingleton<IEmbeddingService, FakeEmbeddingService>();
            services.AddSingleton<IChatCompletionService, PremiumFakeChatCompletionService>();
        }
        else
        {
            services.AddHttpClient<IEmbeddingService, OpenAiEmbeddingService>(ConfigureOpenAiClient);
            services.AddHttpClient<IChatCompletionService, OpenAiChatCompletionService>(ConfigureOpenAiClient);
        }

        services.AddScoped<AuthService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<ChatService>();
        services.AddScoped<ConversationService>();

        return services;
    }

    private static void ConfigureOpenAiClient(IServiceProvider serviceProvider, HttpClient client)
    {
        var options = serviceProvider.GetRequiredService<IOptions<OpenAiOptions>>().Value;
        var baseUrl = options.BaseUrl.EndsWith("/", StringComparison.Ordinal)
            ? options.BaseUrl
            : $"{options.BaseUrl}/";

        client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        if (!string.IsNullOrWhiteSpace(options.ApiKey))
        {
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", options.ApiKey);
        }
    }

    private static string ResolvePostgresConnectionString(IConfiguration configuration)
    {
        var configuredValue = FirstNonEmpty(
            configuration["ROOTFLOW_DATABASE_URL"],
            configuration["DATABASE_URL"],
            configuration.GetConnectionString("Postgres"));

        return NormalizePostgresConnectionString(configuredValue);
    }

    private static string? ResolveOpenAiApiKey(IConfiguration configuration, OpenAiOptions options)
    {
        return FirstNonEmpty(configuration["OPENAI_API_KEY"], configuration["OpenAI:ApiKey"], options.ApiKey);
    }

    private static string NormalizePostgresConnectionString(string? configuredValue)
    {
        if (string.IsNullOrWhiteSpace(configuredValue))
        {
            return string.Empty;
        }

        var trimmed = configuredValue.Trim();
        if (!trimmed.StartsWith("postgres://", StringComparison.OrdinalIgnoreCase) &&
            !trimmed.StartsWith("postgresql://", StringComparison.OrdinalIgnoreCase))
        {
            return trimmed;
        }

        var uri = new Uri(trimmed);
        var userInfo = uri.UserInfo.Split(':', 2, StringSplitOptions.None);
        var builder = new NpgsqlConnectionStringBuilder
        {
            Host = uri.Host,
            Port = uri.IsDefaultPort ? 5432 : uri.Port,
            Database = uri.AbsolutePath.Trim('/'),
            Username = userInfo.Length > 0 ? Uri.UnescapeDataString(userInfo[0]) : string.Empty,
            Password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty
        };

        foreach (var (key, value) in ParseConnectionStringQuery(uri.Query))
        {
            switch (key.ToLowerInvariant())
            {
                case "sslmode":
                    if (Enum.TryParse<SslMode>(value, true, out var sslMode))
                    {
                        builder.SslMode = sslMode;
                    }
                    break;
                case "trustservercertificate":
                case "trust_server_certificate":
                    break;
                case "pooling":
                    if (bool.TryParse(value, out var pooling))
                    {
                        builder.Pooling = pooling;
                    }
                    break;
                case "minpoolsize":
                case "minimum pool size":
                    if (int.TryParse(value, out var minPoolSize))
                    {
                        builder.MinPoolSize = minPoolSize;
                    }
                    break;
                case "maxpoolsize":
                case "maximum pool size":
                    if (int.TryParse(value, out var maxPoolSize))
                    {
                        builder.MaxPoolSize = maxPoolSize;
                    }
                    break;
                case "timeout":
                    if (int.TryParse(value, out var timeout))
                    {
                        builder.Timeout = timeout;
                    }
                    break;
                case "commandtimeout":
                case "command timeout":
                    if (int.TryParse(value, out var commandTimeout))
                    {
                        builder.CommandTimeout = commandTimeout;
                    }
                    break;
            }
        }

        return builder.ConnectionString;
    }

    private static IEnumerable<(string Key, string Value)> ParseConnectionStringQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            yield break;
        }

        foreach (var segment in query.TrimStart('?')
                     .Split('&', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var parts = segment.Split('=', 2, StringSplitOptions.None);
            var key = Uri.UnescapeDataString(parts[0]);
            var value = parts.Length > 1 ? Uri.UnescapeDataString(parts[1]) : string.Empty;

            if (!string.IsNullOrWhiteSpace(key))
            {
                yield return (key, value);
            }
        }
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value))?.Trim();
    }
}
