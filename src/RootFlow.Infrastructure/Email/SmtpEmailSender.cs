using System.Diagnostics;
using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
    private const int DefaultTimeoutMilliseconds = 15000;
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<SmtpEmailSender> _logger;

    public SmtpEmailSender(
        IOptions<EmailDeliveryOptions> options,
        ILogger<SmtpEmailSender> logger)
    {
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.FromAddress) &&
        !string.IsNullOrWhiteSpace(_options.SmtpHost) &&
        _options.SmtpPort > 0;

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("SMTP email delivery is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var settings = ResolveSettings();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var mailMessage = new MailMessage
            {
                From = CreateAddress(settings.FromAddress, settings.FromName),
                Subject = message.Subject,
                SubjectEncoding = Encoding.UTF8,
                BodyEncoding = Encoding.UTF8,
                Body = message.HtmlBody,
                IsBodyHtml = true
            };

            mailMessage.To.Add(CreateAddress(message.ToAddress, message.ToDisplayName));
            mailMessage.AlternateViews.Add(AlternateView.CreateAlternateViewFromString(
                message.PlainTextBody,
                Encoding.UTF8,
                "text/plain"));

            using var smtpClient = new SmtpClient(settings.SmtpHost, settings.SmtpPort)
            {
                DeliveryMethod = SmtpDeliveryMethod.Network,
                EnableSsl = settings.EnableSsl,
                Timeout = settings.TimeoutMilliseconds,
                UseDefaultCredentials = false
            };

            if (!string.IsNullOrWhiteSpace(settings.Username) || !string.IsNullOrWhiteSpace(settings.Password))
            {
                smtpClient.Credentials = new NetworkCredential(
                    settings.Username,
                    settings.Password);
            }

            _logger.LogDebug(
                "Attempting SMTP delivery to {Email} via {SmtpHost}:{SmtpPort} (SSL: {EnableSsl}, TimeoutMs: {TimeoutMs}, UserConfigured: {HasUsername}).",
                message.ToAddress,
                settings.SmtpHost,
                settings.SmtpPort,
                settings.EnableSsl,
                settings.TimeoutMilliseconds,
                !string.IsNullOrWhiteSpace(settings.Username));

            await Task.Run(() => smtpClient.Send(mailMessage), cancellationToken);

            _logger.LogInformation(
                "Delivered RootFlow email to {Email} via {SmtpHost}:{SmtpPort} in {ElapsedMilliseconds} ms.",
                message.ToAddress,
                settings.SmtpHost,
                settings.SmtpPort,
                stopwatch.ElapsedMilliseconds);
        }
        catch (SmtpFailedRecipientException exception)
        {
            _logger.LogError(
                exception,
                "SMTP recipient failure while sending RootFlow email to {Email} via {SmtpHost}:{SmtpPort} after {ElapsedMilliseconds} ms. StatusCode: {StatusCode}. {Hint}",
                message.ToAddress,
                settings.SmtpHost,
                settings.SmtpPort,
                stopwatch.ElapsedMilliseconds,
                exception.StatusCode,
                BuildFailureHint(settings, exception.StatusCode));

            throw;
        }
        catch (SmtpException exception)
        {
            _logger.LogError(
                exception,
                "SMTP delivery failed for {Email} via {SmtpHost}:{SmtpPort} after {ElapsedMilliseconds} ms. StatusCode: {StatusCode}. SSL: {EnableSsl}. Username configured: {HasUsername}. From: {FromAddress}. {Hint}",
                message.ToAddress,
                settings.SmtpHost,
                settings.SmtpPort,
                stopwatch.ElapsedMilliseconds,
                exception.StatusCode,
                settings.EnableSsl,
                !string.IsNullOrWhiteSpace(settings.Username),
                settings.FromAddress,
                BuildFailureHint(settings, exception.StatusCode));

            throw;
        }
        catch (Exception exception) when (exception is InvalidOperationException or FormatException)
        {
            _logger.LogError(
                exception,
                "SMTP configuration failed before sending RootFlow email to {Email}. Host: {SmtpHost}:{SmtpPort}. From: {FromAddress}. Username configured: {HasUsername}. {Hint}",
                message.ToAddress,
                settings.SmtpHost,
                settings.SmtpPort,
                settings.FromAddress,
                !string.IsNullOrWhiteSpace(settings.Username),
                BuildFailureHint(settings, null));

            throw;
        }
    }

    private SmtpSendSettings ResolveSettings()
    {
        var host = _options.SmtpHost.Trim();
        var fromAddress = _options.FromAddress.Trim();
        var fromName = _options.FromName.Trim();
        var username = _options.SmtpUsername.Trim();
        var password = NormalizePassword(host, _options.SmtpPassword);
        var timeoutMilliseconds = _options.SmtpTimeoutMilliseconds > 0
            ? _options.SmtpTimeoutMilliseconds
            : DefaultTimeoutMilliseconds;

        if (IsGmailHost(host))
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                throw new InvalidOperationException(
                    "Gmail SMTP requires ROOTFLOW_EMAIL_SMTP_USERNAME as the full Gmail address and ROOTFLOW_EMAIL_SMTP_PASSWORD as an app password.");
            }

            if (!LooksLikeEmailAddress(username))
            {
                _logger.LogWarning(
                    "Gmail SMTP is configured, but the username does not look like a full email address. Gmail expects the complete Gmail or Google Workspace address.");
            }

            if (!AddressesMatch(fromAddress, username))
            {
                _logger.LogWarning(
                    "Gmail SMTP is configured with ROOTFLOW_EMAIL_FROM_ADDRESS={FromAddress} and ROOTFLOW_EMAIL_SMTP_USERNAME={Username}. Gmail usually expects the authenticated mailbox or a configured alias to send successfully.",
                    fromAddress,
                    username);
            }

            if (_options.SmtpPort is not 465 and not 587)
            {
                _logger.LogWarning(
                    "Gmail SMTP is configured on port {SmtpPort}. Google documents 587 for TLS/STARTTLS and 465 for SSL.",
                    _options.SmtpPort);
            }

            if (!_options.SmtpEnableSsl)
            {
                _logger.LogWarning(
                    "Gmail SMTP is configured with SSL/TLS disabled. Google documents Gmail SMTP with SSL or TLS enabled.");
            }
        }

        return new SmtpSendSettings(
            host,
            _options.SmtpPort,
            username,
            password,
            fromAddress,
            string.IsNullOrWhiteSpace(fromName) ? "RootFlow" : fromName,
            _options.SmtpEnableSsl,
            timeoutMilliseconds);
    }

    private static MailAddress CreateAddress(string address, string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? new MailAddress(address.Trim())
            : new MailAddress(address.Trim(), displayName.Trim(), Encoding.UTF8);
    }

    private static string NormalizePassword(string host, string password)
    {
        var trimmedPassword = password.Trim();
        return IsGmailHost(host) ? trimmedPassword.Replace(" ", string.Empty) : trimmedPassword;
    }

    private static bool LooksLikeEmailAddress(string value)
    {
        try
        {
            _ = new MailAddress(value);
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool AddressesMatch(string left, string right)
    {
        try
        {
            var leftAddress = new MailAddress(left).Address;
            var rightAddress = new MailAddress(right).Address;
            return string.Equals(leftAddress, rightAddress, StringComparison.OrdinalIgnoreCase);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    private static bool IsGmailHost(string host)
    {
        return string.Equals(host, "smtp.gmail.com", StringComparison.OrdinalIgnoreCase);
    }

    private static string BuildFailureHint(SmtpSendSettings settings, SmtpStatusCode? statusCode)
    {
        if (!IsGmailHost(settings.SmtpHost))
        {
            return "Verify the SMTP host, port, SSL/TLS requirement, credentials, and sender address with your provider.";
        }

        var gmailHint = "For Gmail SMTP, use smtp.gmail.com, port 587 with ROOTFLOW_EMAIL_SMTP_ENABLE_SSL=true, the full Gmail address as ROOTFLOW_EMAIL_SMTP_USERNAME, and a Gmail app password as ROOTFLOW_EMAIL_SMTP_PASSWORD.";
        if (statusCode == SmtpStatusCode.MustIssueStartTlsFirst)
        {
            return $"{gmailHint} Google also requires STARTTLS when using port 587.";
        }

        return $"{gmailHint} ROOTFLOW_EMAIL_FROM_ADDRESS should match that mailbox or a configured Gmail alias.";
    }

    private sealed record SmtpSendSettings(
        string SmtpHost,
        int SmtpPort,
        string Username,
        string Password,
        string FromAddress,
        string FromName,
        bool EnableSsl,
        int TimeoutMilliseconds);
}
