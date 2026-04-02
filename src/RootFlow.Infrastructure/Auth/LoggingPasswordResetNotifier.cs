using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Auth;

public sealed class LoggingPasswordResetNotifier : IPasswordResetNotifier
{
    private readonly PasswordResetOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LoggingPasswordResetNotifier> _logger;

    public LoggingPasswordResetNotifier(
        IOptions<PasswordResetOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<LoggingPasswordResetNotifier> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task SendResetLinkAsync(
        PasswordResetNotification notification,
        CancellationToken cancellationToken = default)
    {
        var resetLink = BuildResetLink(notification.Token);
        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Password reset requested for {Email}. Reset link: {ResetLink}. Expires at {ExpiresAtUtc}.",
                notification.Email,
                resetLink,
                notification.ExpiresAtUtc);

            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Password reset requested for {Email}, but no outbound email provider is configured. Replace {NotifierType} with a real email sender before enabling this flow in production.",
            notification.Email,
            nameof(LoggingPasswordResetNotifier));

        return Task.CompletedTask;
    }

    private string BuildResetLink(string token)
    {
        var baseUrl = ResolveFrontendBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"/auth/reset-password?token={Uri.EscapeDataString(token)}";
        }

        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : $"{baseUrl}/";

        return $"{normalizedBaseUrl}auth/reset-password?token={Uri.EscapeDataString(token)}";
    }

    private string ResolveFrontendBaseUrl()
    {
        if (!string.IsNullOrWhiteSpace(_options.FrontendBaseUrl))
        {
            return _options.FrontendBaseUrl.Trim();
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            return "http://localhost:5173";
        }

        return string.Empty;
    }
}
