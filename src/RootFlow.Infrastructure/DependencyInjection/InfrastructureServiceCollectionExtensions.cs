using System.Net.Http.Headers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Npgsql;
using Pgvector;
using RootFlow.Application.Abstractions.AI;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Abstractions.Documents;
using RootFlow.Application.Abstractions.Persistence;
using RootFlow.Application.Abstractions.Search;
using RootFlow.Application.Abstractions.Time;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Application.Auth;
using RootFlow.Application.Billing;
using RootFlow.Application.Chat;
using RootFlow.Application.Conversations;
using RootFlow.Application.Documents;
using RootFlow.Application.PlatformAdmin;
using RootFlow.Application.Workspaces;
using RootFlow.Infrastructure.AI;
using RootFlow.Infrastructure.Auth;
using RootFlow.Infrastructure.Billing;
using RootFlow.Infrastructure.Configuration;
using RootFlow.Infrastructure.Documents;
using RootFlow.Infrastructure.Email;
using RootFlow.Infrastructure.Persistence;
using RootFlow.Infrastructure.Search;
using RootFlow.Infrastructure.Storage;
using RootFlow.Infrastructure.Time;
using RootFlow.Infrastructure.Workspaces;

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
        services.Configure<WorkspaceBillingOptions>(configuration.GetSection("Billing"));
        services.Configure<PlatformAdminOptions>(configuration.GetSection("PlatformAdmin"));
        services.Configure<StripeBillingOptions>(configuration.GetSection("Stripe"));
        services.Configure<EmailDeliveryOptions>(configuration.GetSection("EmailDelivery"));
        services.Configure<OpenAiOptions>(configuration.GetSection("OpenAI"));
        services.Configure<PasswordResetOptions>(configuration.GetSection("PasswordReset"));
        services.Configure<StorageOptions>(configuration.GetSection("Storage"));
        services.Configure<TextChunkingOptions>(configuration.GetSection("Chunking"));
        services.Configure<WorkspaceInvitationOptions>(configuration.GetSection("WorkspaceInvitations"));
        services.PostConfigure<OpenAiOptions>(options =>
        {
            options.ApiKey = ResolveOpenAiApiKey(configuration, options);
        });
        services.PostConfigure<PasswordResetOptions>(options =>
        {
            options.FrontendBaseUrl = FirstNonEmpty(
                configuration["ROOTFLOW_FRONTEND_BASE_URL"],
                configuration["PasswordReset:FrontendBaseUrl"],
                options.FrontendBaseUrl) ?? string.Empty;
        });
        services.PostConfigure<WorkspaceInvitationOptions>(options =>
        {
            options.FrontendBaseUrl = FirstNonEmpty(
                configuration["ROOTFLOW_FRONTEND_BASE_URL"],
                configuration["WorkspaceInvitations:FrontendBaseUrl"],
                options.FrontendBaseUrl) ?? string.Empty;
        });
        services.PostConfigure<StripeBillingOptions>(options =>
        {
            options.SecretKey = FirstNonEmpty(
                configuration["ROOTFLOW_STRIPE_SECRET_KEY"],
                configuration["Stripe:SecretKey"],
                options.SecretKey) ?? string.Empty;
            options.WebhookSecret = FirstNonEmpty(
                configuration["ROOTFLOW_STRIPE_WEBHOOK_SECRET"],
                configuration["Stripe:WebhookSecret"],
                options.WebhookSecret) ?? string.Empty;

            ApplyStripePlanPriceOverride(options, "starter", configuration["ROOTFLOW_STRIPE_STARTER_PRICE_ID"]);
            ApplyStripePlanPriceOverride(options, "pro", configuration["ROOTFLOW_STRIPE_PRO_PRICE_ID"]);
            ApplyStripePlanPriceOverride(options, "business", configuration["ROOTFLOW_STRIPE_BUSINESS_PRICE_ID"]);
            ApplyStripeCreditPackPriceOverride(options, "credits_10000", configuration["ROOTFLOW_STRIPE_CREDITS_10000_PRICE_ID"]);
        });
        services.PostConfigure<PlatformAdminOptions>(options =>
        {
            var configuredEmails = SplitDelimitedValues(configuration["ROOTFLOW_PLATFORM_ADMIN_EMAILS"]);
            if (configuredEmails.Length == 0)
            {
                return;
            }

            options.Emails = configuredEmails.ToList();
        });
        services.PostConfigure<EmailDeliveryOptions>(options =>
        {
            options.Provider = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_PROVIDER"],
                configuration["EmailDelivery:Provider"],
                options.Provider) ?? options.Provider;
            options.FromAddress = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_FROM_ADDRESS"],
                configuration["EmailDelivery:FromAddress"],
                options.FromAddress) ?? string.Empty;
            options.FromName = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_FROM_NAME"],
                configuration["EmailDelivery:FromName"],
                options.FromName) ?? options.FromName;
            options.SmtpHost = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_SMTP_HOST"],
                configuration["EmailDelivery:SmtpHost"],
                options.SmtpHost) ?? string.Empty;
            options.SmtpPort = FirstParsedInt(
                    configuration["ROOTFLOW_EMAIL_SMTP_PORT"],
                    configuration["EmailDelivery:SmtpPort"])
                ?? options.SmtpPort;
            options.SmtpUsername = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_SMTP_USERNAME"],
                configuration["EmailDelivery:SmtpUsername"],
                options.SmtpUsername) ?? string.Empty;
            options.SmtpPassword = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_SMTP_PASSWORD"],
                configuration["EmailDelivery:SmtpPassword"],
                options.SmtpPassword) ?? string.Empty;
            options.SmtpEnableSsl = FirstParsedBool(
                    configuration["ROOTFLOW_EMAIL_SMTP_ENABLE_SSL"],
                    configuration["EmailDelivery:SmtpEnableSsl"])
                ?? options.SmtpEnableSsl;
            options.SmtpTimeoutMilliseconds = FirstParsedInt(
                    configuration["ROOTFLOW_EMAIL_SMTP_TIMEOUT_MS"],
                    configuration["EmailDelivery:SmtpTimeoutMilliseconds"])
                ?? options.SmtpTimeoutMilliseconds;
            options.ResendApiKey = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_RESEND_API_KEY"],
                configuration["EmailDelivery:ResendApiKey"],
                options.ResendApiKey) ?? string.Empty;
            options.ResendBaseUrl = FirstNonEmpty(
                configuration["ROOTFLOW_EMAIL_RESEND_BASE_URL"],
                configuration["EmailDelivery:ResendBaseUrl"],
                options.ResendBaseUrl) ?? options.ResendBaseUrl;
        });

        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<WorkspaceBillingOptions>>().Value);
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<PlatformAdminOptions>>().Value);
        services.AddSingleton(serviceProvider =>
            serviceProvider.GetRequiredService<IOptions<StripeBillingOptions>>().Value);
        services.AddSingleton(_ =>
        {
            var builder = new NpgsqlDataSourceBuilder(connectionString);
            builder.UseVector();
            return builder.Build();
        });

        services.AddSingleton<PostgresDatabaseInitializer>();
        services.AddSingleton<RootFlowAppLinkBuilder>();
        services.AddSingleton<IAppLinkBuilder>(serviceProvider => serviceProvider.GetRequiredService<RootFlowAppLinkBuilder>());

        services.AddScoped<IFileStorage, LocalFileStorage>();
        services.AddScoped<IDocumentTextExtractor, SimpleDocumentTextExtractor>();
        services.AddScoped<ITextChunker, SimpleTextChunker>();
        services.AddScoped<SmtpEmailSender>();
        services.AddHttpClient<ResendEmailSender>((serviceProvider, client) =>
        {
            var options = serviceProvider.GetRequiredService<IOptions<EmailDeliveryOptions>>().Value;
            var baseUrl = options.ResendBaseUrl.EndsWith("/", StringComparison.Ordinal)
                ? options.ResendBaseUrl
                : $"{options.ResendBaseUrl}/";

            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.Timeout = ResendEmailSender.ResolveTimeout(options);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RootFlow/1.0");
        });
        services.AddScoped<ConfigurableEmailSender>();
        services.AddScoped<IEmailSender>(serviceProvider => serviceProvider.GetRequiredService<ConfigurableEmailSender>());
        services.AddHttpClient<IStripePaymentGateway, StripePaymentGateway>(client =>
        {
            client.BaseAddress = new Uri("https://api.stripe.com/", UriKind.Absolute);
            client.DefaultRequestHeaders.UserAgent.ParseAdd("RootFlow/1.0");
        });

        services.AddScoped<IPasswordHashingService, AspNetPasswordHashingService>();
        services.AddSingleton<IPlatformAdminAccessService, ConfiguredPlatformAdminAccessService>();
        services.AddScoped<IPasswordResetNotifier, LoggingPasswordResetNotifier>();
        services.AddScoped<IWorkspaceInvitationNotifier, LoggingWorkspaceInvitationNotifier>();
        services.AddScoped<IAiUsagePricingCalculator, ConfiguredAiUsagePricingCalculator>();
        services.AddScoped<IAuthRepository, PostgresAuthRepository>();
        services.AddScoped<IBillingPlanRepository, PostgresBillingPlanRepository>();
        services.AddScoped<IPlatformAdminRepository, PostgresPlatformAdminRepository>();
        services.AddScoped<IWorkspaceBillingRepository, PostgresWorkspaceBillingRepository>();
        services.AddScoped<IWorkspaceInvitationRepository, PostgresWorkspaceInvitationRepository>();
        services.AddScoped<IWorkspaceMembershipRepository, PostgresWorkspaceMembershipRepository>();
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
        services.AddScoped<PlatformAdminDashboardService>();
        services.AddScoped<WorkspaceBillingService>();
        services.AddScoped<WorkspacePaymentService>();
        services.AddScoped<DocumentService>();
        services.AddScoped<ChatService>();
        services.AddScoped<ConversationService>();
        services.AddScoped<WorkspaceCollaborationService>();

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

    private static string[] SplitDelimitedValues(string? value)
    {
        return string.IsNullOrWhiteSpace(value)
            ? []
            : value
                .Split([',', ';'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    private static int? FirstParsedInt(params string?[] values)
    {
        foreach (var value in values)
        {
            if (int.TryParse(value, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    private static bool? FirstParsedBool(params string?[] values)
    {
        foreach (var value in values)
        {
            if (bool.TryParse(value, out var parsedValue))
            {
                return parsedValue;
            }
        }

        return null;
    }

    private static void ApplyStripePlanPriceOverride(
        StripeBillingOptions options,
        string planCode,
        string? priceId)
    {
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return;
        }

        var existing = options.PlanPrices.FirstOrDefault(option =>
            string.Equals(option.PlanCode, planCode, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.PriceId = priceId.Trim();
            return;
        }

        options.PlanPrices.Add(new StripePlanPriceOptions
        {
            PlanCode = planCode,
            PriceId = priceId.Trim()
        });
    }

    private static void ApplyStripeCreditPackPriceOverride(
        StripeBillingOptions options,
        string creditPackCode,
        string? priceId)
    {
        if (string.IsNullOrWhiteSpace(priceId))
        {
            return;
        }

        var existing = options.CreditPacks.FirstOrDefault(option =>
            string.Equals(option.Code, creditPackCode, StringComparison.OrdinalIgnoreCase));

        if (existing is not null)
        {
            existing.PriceId = priceId.Trim();
            return;
        }

        options.CreditPacks.Add(new StripeCreditPackOptions
        {
            Code = creditPackCode,
            Name = "10,000 credits",
            Description = "Extra shared credits for the workspace.",
            Credits = 10_000,
            Amount = 49.90m,
            CurrencyCode = "BRL",
            PriceId = priceId.Trim()
        });
    }
}
