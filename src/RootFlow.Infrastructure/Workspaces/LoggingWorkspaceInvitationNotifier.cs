using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Workspaces;

public sealed class LoggingWorkspaceInvitationNotifier : IWorkspaceInvitationNotifier
{
    private readonly WorkspaceInvitationOptions _options;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LoggingWorkspaceInvitationNotifier> _logger;

    public LoggingWorkspaceInvitationNotifier(
        IOptions<WorkspaceInvitationOptions> options,
        IHostEnvironment hostEnvironment,
        ILogger<LoggingWorkspaceInvitationNotifier> logger)
    {
        _options = options.Value;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task SendInviteLinkAsync(
        WorkspaceInvitationNotification notification,
        CancellationToken cancellationToken = default)
    {
        var inviteLink = BuildInviteLink(notification.Token);
        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Workspace invite requested for {Email}. Invite link: {InviteLink}. Workspace: {WorkspaceName}. Role: {Role}. Expires at {ExpiresAtUtc}.",
                notification.Email,
                inviteLink,
                notification.WorkspaceName,
                notification.Role,
                notification.ExpiresAtUtc);

            return Task.CompletedTask;
        }

        _logger.LogWarning(
            "Workspace invite requested for {Email}, but no outbound email provider is configured. Replace {NotifierType} with a real email sender before enabling invites in production.",
            notification.Email,
            nameof(LoggingWorkspaceInvitationNotifier));

        return Task.CompletedTask;
    }

    private string BuildInviteLink(string token)
    {
        var baseUrl = ResolveFrontendBaseUrl();
        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            return $"/auth/invite?token={Uri.EscapeDataString(token)}";
        }

        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : $"{baseUrl}/";

        return $"{normalizedBaseUrl}auth/invite?token={Uri.EscapeDataString(token)}";
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
