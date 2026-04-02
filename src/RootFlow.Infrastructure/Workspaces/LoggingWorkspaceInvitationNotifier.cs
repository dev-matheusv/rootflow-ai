using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Workspaces;
using RootFlow.Infrastructure.Email;

namespace RootFlow.Infrastructure.Workspaces;

public sealed class LoggingWorkspaceInvitationNotifier : IWorkspaceInvitationNotifier
{
    private readonly IEmailSender _emailSender;
    private readonly RootFlowAppLinkBuilder _appLinkBuilder;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LoggingWorkspaceInvitationNotifier> _logger;

    public LoggingWorkspaceInvitationNotifier(
        IEmailSender emailSender,
        RootFlowAppLinkBuilder appLinkBuilder,
        IHostEnvironment hostEnvironment,
        ILogger<LoggingWorkspaceInvitationNotifier> logger)
    {
        _emailSender = emailSender;
        _appLinkBuilder = appLinkBuilder;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task SendInviteLinkAsync(
        WorkspaceInvitationNotification notification,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(notification, cancellationToken);
    }

    private async Task SendAsync(
        WorkspaceInvitationNotification notification,
        CancellationToken cancellationToken)
    {
        var inviteLink = BuildInviteLink(notification.Token, requireAbsoluteUrl: _emailSender.IsConfigured);
        if (_emailSender.IsConfigured)
        {
            var inviterName = string.IsNullOrWhiteSpace(notification.InvitedByFullName)
                ? "A RootFlow workspace admin"
                : notification.InvitedByFullName.Trim();

            await _emailSender.SendAsync(
                RootFlowEmailTemplate.CreateMessage(
                    notification.Email,
                    null,
                    new ActionEmailTemplate(
                        $"You're invited to join {notification.WorkspaceName} on RootFlow",
                        $"Accept your invite to collaborate in {notification.WorkspaceName} on RootFlow.",
                        "Workspace invite",
                        $"Join {notification.WorkspaceName}",
                        $"{inviterName} invited you to collaborate in the {notification.WorkspaceName} workspace on RootFlow.",
                        "Accept invite",
                        inviteLink,
                        [
                            $"Role: {notification.Role}",
                            $"This invitation expires on {FormatTimestamp(notification.ExpiresAtUtc)}."
                        ],
                        "Accept the invite using the same email address that received it. If you need a new link, ask your workspace admin to resend the invite.",
                        "RootFlow workspace invites are single-use and switch your session into the invited workspace after acceptance.")),
                cancellationToken);

            return;
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Workspace invite requested for {Email}. Invite link: {InviteLink}. Workspace: {WorkspaceName}. Role: {Role}. Expires at {ExpiresAtUtc}.",
                notification.Email,
                inviteLink,
                notification.WorkspaceName,
                notification.Role,
                notification.ExpiresAtUtc);

            return;
        }

        _logger.LogWarning(
            "Workspace invite requested for {Email}, but outbound email is not configured. Set the ROOTFLOW_EMAIL_* variables to enable real delivery.",
            notification.Email);
    }

    private string BuildInviteLink(string token, bool requireAbsoluteUrl)
    {
        return _appLinkBuilder.BuildWorkspaceInviteLink(token, requireAbsoluteUrl);
    }

    private static string FormatTimestamp(DateTime value)
    {
        return value.ToUniversalTime().ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'", CultureInfo.InvariantCulture);
    }
}
