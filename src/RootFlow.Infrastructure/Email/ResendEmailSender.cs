using System.Diagnostics;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using RootFlow.Infrastructure.Configuration;

namespace RootFlow.Infrastructure.Email;

public sealed class ResendEmailSender : IEmailSender
{
    private const int DefaultTimeoutMilliseconds = 15000;
    private readonly HttpClient _httpClient;
    private readonly EmailDeliveryOptions _options;
    private readonly ILogger<ResendEmailSender> _logger;

    public ResendEmailSender(
        HttpClient httpClient,
        IOptions<EmailDeliveryOptions> options,
        ILogger<ResendEmailSender> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_options.FromAddress) &&
        !string.IsNullOrWhiteSpace(_options.ResendApiKey);

    public async Task SendAsync(EmailMessage message, CancellationToken cancellationToken = default)
    {
        if (!IsConfigured)
        {
            throw new InvalidOperationException("Resend email delivery is not configured.");
        }

        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();

        using var request = new HttpRequestMessage(HttpMethod.Post, "emails");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.ResendApiKey.Trim());
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        request.Headers.TryAddWithoutValidation("Idempotency-Key", Guid.NewGuid().ToString("N"));
        request.Content = JsonContent.Create(new
        {
            from = FormatMailbox(_options.FromAddress, _options.FromName),
            to = new[] { FormatMailbox(message.ToAddress, message.ToDisplayName) },
            subject = message.Subject,
            html = message.HtmlBody,
            text = message.PlainTextBody
        });

        try
        {
            _logger.LogDebug(
                "Attempting Resend delivery to {Email} using base URL {BaseUrl}.",
                message.ToAddress,
                _httpClient.BaseAddress);

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            var responseBody = await response.Content.ReadAsStringAsync(cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    "Resend delivery failed for {Email} after {ElapsedMilliseconds} ms. Status: {StatusCode}. Response: {ResponseBody}",
                    message.ToAddress,
                    stopwatch.ElapsedMilliseconds,
                    (int)response.StatusCode,
                    Truncate(responseBody, 1000));

                throw new InvalidOperationException(
                    $"Resend email delivery failed with status {(int)response.StatusCode}. Check API key, sender domain, and provider logs.");
            }

            var payload = JsonSerializer.Deserialize<ResendSendEmailResponse>(responseBody);

            _logger.LogInformation(
                "Delivered RootFlow email to {Email} through Resend in {ElapsedMilliseconds} ms. MessageId: {MessageId}",
                message.ToAddress,
                stopwatch.ElapsedMilliseconds,
                payload?.Id ?? "unknown");
        }
        catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException or InvalidOperationException)
        {
            _logger.LogError(
                exception,
                "Resend delivery failed for {Email} after {ElapsedMilliseconds} ms. From: {FromAddress}. Base URL: {BaseUrl}. Ensure ROOTFLOW_EMAIL_RESEND_API_KEY is valid and ROOTFLOW_EMAIL_FROM_ADDRESS is a verified sender.",
                message.ToAddress,
                stopwatch.ElapsedMilliseconds,
                _options.FromAddress,
                _httpClient.BaseAddress);

            throw;
        }
    }

    public static TimeSpan ResolveTimeout(EmailDeliveryOptions options)
    {
        var timeoutMilliseconds = options.SmtpTimeoutMilliseconds > 0
            ? options.SmtpTimeoutMilliseconds
            : DefaultTimeoutMilliseconds;

        return TimeSpan.FromMilliseconds(timeoutMilliseconds);
    }

    private static string FormatMailbox(string address, string? displayName)
    {
        return string.IsNullOrWhiteSpace(displayName)
            ? address.Trim()
            : $"{displayName.Trim()} <{address.Trim()}>";
    }

    private static string Truncate(string value, int maxLength)
    {
        return value.Length <= maxLength ? value : $"{value[..maxLength]}...";
    }

    private sealed record ResendSendEmailResponse(string Id);
}
