using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Email;

public sealed class ConfigurableEmailSender : IEmailSender
{
    private readonly EmailDeliveryOptions _options;
    private readonly SmtpEmailSender _smtpEmailSender;
    private readonly ResendEmailSender _resendEmailSender;
    private readonly ILogger<ConfigurableEmailSender> _logger;

    public ConfigurableEmailSender(
        IOptions<EmailDeliveryOptions> options,
        SmtpEmailSender smtpEmailSender,
        ResendEmailSender resendEmailSender,
        ILogger<ConfigurableEmailSender> logger)
    {
        _options = options.Value;
        _smtpEmailSender = smtpEmailSender;
        _resendEmailSender = resendEmailSender;
        _logger = logger;
    }

    public bool IsConfigured => ResolveActiveSender().IsConfigured;

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        return ResolveActiveSender().SendAsync(message, cancellationToken);
    }

    private IEmailSender ResolveActiveSender()
    {
        var provider = (_options.Provider ?? string.Empty).Trim();
        if (string.IsNullOrWhiteSpace(provider) || string.Equals(provider, "smtp", StringComparison.OrdinalIgnoreCase))
        {
            return _smtpEmailSender;
        }

        if (string.Equals(provider, "resend", StringComparison.OrdinalIgnoreCase))
        {
            return _resendEmailSender;
        }

        _logger.LogWarning(
            "Unknown email provider {Provider}. Falling back to SMTP.",
            provider);

        return _smtpEmailSender;
    }
}
