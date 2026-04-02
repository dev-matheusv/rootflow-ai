namespace RootFlow.Infrastructure.Configuration;

public sealed class EmailDeliveryOptions
{
    public string Provider { get; set; } = "Smtp";

    public string FromAddress { get; set; } = string.Empty;

    public string FromName { get; set; } = "RootFlow";

    public string SmtpHost { get; set; } = string.Empty;

    public int SmtpPort { get; set; } = 587;

    public string SmtpUsername { get; set; } = string.Empty;

    public string SmtpPassword { get; set; } = string.Empty;

    public bool SmtpEnableSsl { get; set; } = true;

    public int SmtpTimeoutMilliseconds { get; set; } = 15000;

    public string ResendApiKey { get; set; } = string.Empty;

    public string ResendBaseUrl { get; set; } = "https://api.resend.com/";
}
