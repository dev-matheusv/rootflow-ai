using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Chat;
using RootFlow.Application.Conversations;
using RootFlow.Application.Documents;
using RootFlow.Infrastructure.AI;
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
        var connectionString = configuration.GetConnectionString("Postgres");
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException("Connection string 'Postgres' is not configured.");
        }

        services.Configure<AiOptions>(configuration.GetSection("AI"));
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<TextChunkingOptions>(configuration.GetSection("Chunking"));
        services.Configure<RootFlowOptions>(configuration.GetSection("RootFlow"));
        services.PostConfigure<OpenAiOptions>(options =>
        {
            options.ApiKey ??= Environment.GetEnvironmentVariable("OPENAI_API_KEY");
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
}
