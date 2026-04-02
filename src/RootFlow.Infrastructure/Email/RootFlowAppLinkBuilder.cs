using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Email;

public sealed class RootFlowAppLinkBuilder
{
    private const string LocalFrontendBaseUrl = "http://localhost:5173";
    private readonly PasswordResetOptions _passwordResetOptions;
    private readonly WorkspaceInvitationOptions _workspaceInvitationOptions;
    private readonly IHostEnvironment _hostEnvironment;

    public RootFlowAppLinkBuilder(
        IOptions<PasswordResetOptions> passwordResetOptions,
        IOptions<WorkspaceInvitationOptions> workspaceInvitationOptions,
        IHostEnvironment hostEnvironment)
    {
        _passwordResetOptions = passwordResetOptions.Value;
        _workspaceInvitationOptions = workspaceInvitationOptions.Value;
        _hostEnvironment = hostEnvironment;
    }

    public string BuildPasswordResetLink(string token, bool requireAbsoluteUrl = false)
    {
        return BuildLink(
            _passwordResetOptions.FrontendBaseUrl,
            "/auth/reset-password",
            token,
            "ROOTFLOW_FRONTEND_BASE_URL or PasswordReset:FrontendBaseUrl must be configured to send password reset emails.",
            requireAbsoluteUrl);
    }

    public string BuildWorkspaceInviteLink(string token, bool requireAbsoluteUrl = false)
    {
        return BuildLink(
            _workspaceInvitationOptions.FrontendBaseUrl,
            "/auth/invite",
            token,
            "ROOTFLOW_FRONTEND_BASE_URL or WorkspaceInvitations:FrontendBaseUrl must be configured to send workspace invite emails.",
            requireAbsoluteUrl);
    }

    private string BuildLink(
        string configuredBaseUrl,
        string routePath,
        string token,
        string missingConfigurationMessage,
        bool requireAbsoluteUrl)
    {
        var baseUrl = ResolveFrontendBaseUrl(configuredBaseUrl);
        var normalizedRoutePath = routePath.TrimStart('/');
        var query = $"token={Uri.EscapeDataString(token)}";

        if (string.IsNullOrWhiteSpace(baseUrl))
        {
            if (requireAbsoluteUrl)
            {
                throw new InvalidOperationException(missingConfigurationMessage);
            }

            return $"{routePath}?{query}";
        }

        var normalizedBaseUrl = baseUrl.EndsWith("/", StringComparison.Ordinal)
            ? baseUrl
            : $"{baseUrl}/";

        return $"{normalizedBaseUrl}{normalizedRoutePath}?{query}";
    }

    private string ResolveFrontendBaseUrl(string configuredBaseUrl)
    {
        if (!string.IsNullOrWhiteSpace(configuredBaseUrl))
        {
            return configuredBaseUrl.Trim();
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            return LocalFrontendBaseUrl;
        }

        return string.Empty;
    }
}
