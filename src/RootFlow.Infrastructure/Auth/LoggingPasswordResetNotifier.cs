using System.Globalization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using RootFlow.Application.Abstractions.Auth;
using RootFlow.Infrastructure.Email;

namespace RootFlow.Infrastructure.Auth;

public sealed class LoggingPasswordResetNotifier : IPasswordResetNotifier
{
    private readonly IEmailSender _emailSender;
    private readonly RootFlowAppLinkBuilder _appLinkBuilder;
    private readonly IHostEnvironment _hostEnvironment;
    private readonly ILogger<LoggingPasswordResetNotifier> _logger;

    public LoggingPasswordResetNotifier(
        IEmailSender emailSender,
        RootFlowAppLinkBuilder appLinkBuilder,
        IHostEnvironment hostEnvironment,
        ILogger<LoggingPasswordResetNotifier> logger)
    {
        _emailSender = emailSender;
        _appLinkBuilder = appLinkBuilder;
        _hostEnvironment = hostEnvironment;
        _logger = logger;
    }

    public Task SendResetLinkAsync(
        PasswordResetNotification notification,
        CancellationToken cancellationToken = default)
    {
        return SendAsync(notification, cancellationToken);
    }

    private async Task SendAsync(
        PasswordResetNotification notification,
        CancellationToken cancellationToken)
    {
        var resetLink = BuildResetLink(notification.Token, requireAbsoluteUrl: _emailSender.IsConfigured);
        if (_emailSender.IsConfigured)
        {
            await _emailSender.SendAsync(
                RootFlowEmailTemplate.CreateMessage(
                    notification.Email,
                    notification.FullName,
                    new ActionEmailTemplate(
                        "Reset your RootFlow password",
                        "Use this secure link to reset your RootFlow password.",
                        "Password reset",
                        "Reset your password",
                        "We received a request to reset the password for your RootFlow account.",
                        "Reset password",
                        resetLink,
                        [
                            $"This secure link expires on {FormatTimestamp(notification.ExpiresAtUtc)}.",
                            "If you did not request a password reset, you can safely ignore this email."
                        ],
                        "If the button does not open, copy and paste the direct link below into your browser.",
                        "RootFlow password reset links are single-use and time-bound for security.")),
                cancellationToken);

            return;
        }

        if (_hostEnvironment.IsDevelopment() || _hostEnvironment.IsEnvironment("IntegrationTesting"))
        {
            _logger.LogInformation(
                "Password reset requested for {Email}. Reset link: {ResetLink}. Expires at {ExpiresAtUtc}.",
                notification.Email,
                resetLink,
                notification.ExpiresAtUtc);

            return;
        }

        _logger.LogWarning(
            "Password reset requested for {Email}, but outbound email is not configured. Set the ROOTFLOW_EMAIL_* variables to enable real delivery.",
            notification.Email);
    }

    private string BuildResetLink(string token, bool requireAbsoluteUrl)
    {
        return _appLinkBuilder.BuildPasswordResetLink(token, requireAbsoluteUrl);
    }

    private static string FormatTimestamp(DateTime value)
    {
        return value.ToUniversalTime().ToString("MMMM d, yyyy 'at' h:mm tt 'UTC'", CultureInfo.InvariantCulture);
    }
}
