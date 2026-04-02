using System.Net;
using System.Net.Mail;
using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Email;

public sealed class SmtpEmailSender : IEmailSender
{
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

        try
        {
            using var mailMessage = new MailMessage
            {
                From = CreateAddress(_options.FromAddress, _options.FromName),
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

            using var smtpClient = new SmtpClient(_options.SmtpHost.Trim(), _options.SmtpPort)
            {
                EnableSsl = _options.SmtpEnableSsl
            };

            if (!string.IsNullOrWhiteSpace(_options.SmtpUsername) || !string.IsNullOrWhiteSpace(_options.SmtpPassword))
            {
                smtpClient.Credentials = new NetworkCredential(
                    _options.SmtpUsername.Trim(),
                    _options.SmtpPassword);
            }

            await smtpClient.SendMailAsync(mailMessage);
        }
        catch (Exception exception) when (exception is SmtpException or InvalidOperationException or FormatException)
        {
            _logger.LogError(
                exception,
                "Failed to send RootFlow email to {Email} through SMTP host {SmtpHost}.",
                message.ToAddress,
                _options.SmtpHost);

            throw;
        }
    }

    private static MailAddress CreateAddress(string address, string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? new MailAddress(address.Trim())
            : new MailAddress(address.Trim(), displayName.Trim(), Encoding.UTF8);
    }
}
