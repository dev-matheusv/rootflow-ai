using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Options;
using RootFlow.Application.Abstractions.Billing;
using RootFlow.Application.Billing;

namespace RootFlow.Infrastructure.Billing;

public sealed class StripePaymentGateway : IStripePaymentGateway
{
    private readonly HttpClient _httpClient;
    private readonly StripeBillingOptions _options;

    public StripePaymentGateway(
        HttpClient httpClient,
        IOptions<StripeBillingOptions> options)
    {
        _httpClient = httpClient;
        _options = options.Value;
    }

    public Task<StripeCheckoutSessionResult> CreateSubscriptionCheckoutSessionAsync(
        StripeSubscriptionCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>
        {
            ["mode"] = "subscription",
            ["success_url"] = request.SuccessUrl,
            ["cancel_url"] = request.CancelUrl,
            ["line_items[0][price]"] = request.PriceId,
            ["line_items[0][quantity]"] = "1",
            ["metadata[workspace_id]"] = request.WorkspaceId.ToString(),
            ["metadata[billing_plan_code]"] = request.PlanCode,
            ["metadata[checkout_kind]"] = "subscription",
            ["subscription_data[metadata][workspace_id]"] = request.WorkspaceId.ToString(),
            ["subscription_data[metadata][billing_plan_code]"] = request.PlanCode,
            ["subscription_data[metadata][checkout_kind]"] = "subscription"
        };

        if (!string.IsNullOrWhiteSpace(request.ExistingCustomerId))
        {
            values["customer"] = request.ExistingCustomerId!;
        }

        return CreateCheckoutSessionAsync(values, cancellationToken);
    }

    public Task<StripeCheckoutSessionResult> CreateCreditPurchaseCheckoutSessionAsync(
        StripeCreditPurchaseCheckoutRequest request,
        CancellationToken cancellationToken = default)
    {
        var values = new Dictionary<string, string>
        {
            ["mode"] = "payment",
            ["success_url"] = request.SuccessUrl,
            ["cancel_url"] = request.CancelUrl,
            ["line_items[0][price]"] = request.PriceId,
            ["line_items[0][quantity]"] = "1",
            ["metadata[workspace_id]"] = request.WorkspaceId.ToString(),
            ["metadata[credit_pack_code]"] = request.CreditPackCode,
            ["metadata[credit_amount]"] = request.Credits.ToString(CultureInfo.InvariantCulture),
            ["metadata[checkout_kind]"] = "credit_purchase",
            ["payment_intent_data[metadata][workspace_id]"] = request.WorkspaceId.ToString(),
            ["payment_intent_data[metadata][credit_pack_code]"] = request.CreditPackCode,
            ["payment_intent_data[metadata][credit_amount]"] = request.Credits.ToString(CultureInfo.InvariantCulture),
            ["payment_intent_data[metadata][checkout_kind]"] = "credit_purchase"
        };

        if (!string.IsNullOrWhiteSpace(request.ExistingCustomerId))
        {
            values["customer"] = request.ExistingCustomerId!;
        }

        return CreateCheckoutSessionAsync(values, cancellationToken);
    }

    public async Task<StripeSubscriptionSnapshot> GetSubscriptionAsync(
        string subscriptionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(subscriptionId))
        {
            throw new ArgumentException("Stripe subscription id is required.", nameof(subscriptionId));
        }

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new BillingCheckoutUnavailableException("Stripe checkout is not configured.");
        }

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v1/subscriptions/{Uri.EscapeDataString(subscriptionId.Trim())}");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new InvalidOperationException(
                $"Stripe subscription lookup failed with status {(int)response.StatusCode}.");
        }

        using var document = JsonDocument.Parse(payload);
        return ParseSubscriptionSnapshot(document.RootElement);
    }

    public async Task<string?> GetCheckoutSessionSubscriptionIdAsync(
        string checkoutSessionId,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(checkoutSessionId))
            throw new ArgumentException("Stripe checkout session id is required.", nameof(checkoutSessionId));

        if (string.IsNullOrWhiteSpace(_options.SecretKey))
            throw new BillingCheckoutUnavailableException("Stripe checkout is not configured.");

        using var request = new HttpRequestMessage(
            HttpMethod.Get,
            $"v1/checkout/sessions/{Uri.EscapeDataString(checkoutSessionId.Trim())}?expand[]=subscription");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
            return null;

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;

        if (root.TryGetProperty("subscription", out var subscriptionEl))
        {
            if (subscriptionEl.ValueKind == JsonValueKind.String)
                return subscriptionEl.GetString();

            if (subscriptionEl.ValueKind == JsonValueKind.Object &&
                subscriptionEl.TryGetProperty("id", out var idEl))
                return idEl.GetString();
        }

        return null;
    }

    public StripeWebhookEvent ParseWebhook(string payload, string signatureHeader)
    {
        if (string.IsNullOrWhiteSpace(signatureHeader))
        {
            throw new BillingWebhookValidationException("Stripe webhook signature is missing.");
        }

        if (string.IsNullOrWhiteSpace(_options.WebhookSecret))
        {
            throw new BillingWebhookValidationException("Stripe webhook secret is not configured.");
        }

        ValidateSignature(payload, signatureHeader, _options.WebhookSecret, _options.WebhookToleranceSeconds);

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var eventId = root.GetProperty("id").GetString() ?? throw new BillingWebhookValidationException("Stripe event id is missing.");
        var eventType = root.GetProperty("type").GetString() ?? throw new BillingWebhookValidationException("Stripe event type is missing.");
        var occurredAtUtc = DateTimeOffset.FromUnixTimeSeconds(root.GetProperty("created").GetInt64()).UtcDateTime;
        var dataObject = root.GetProperty("data").GetProperty("object");

        return eventType switch
        {
            "checkout.session.completed" => ParseCheckoutCompletedEvent(eventId, occurredAtUtc, dataObject),
            "invoice.paid" => ParseInvoicePaidEvent(eventId, occurredAtUtc, dataObject),
            "invoice.payment_succeeded" => ParseInvoicePaidEvent(eventId, occurredAtUtc, dataObject),
            "customer.subscription.created" => ParseSubscriptionUpdatedEvent(eventId, eventType, occurredAtUtc, dataObject),
            "customer.subscription.updated" => ParseSubscriptionUpdatedEvent(eventId, eventType, occurredAtUtc, dataObject),
            "customer.subscription.deleted" => ParseSubscriptionUpdatedEvent(eventId, eventType, occurredAtUtc, dataObject),
            _ => new StripeUnhandledWebhookEvent(eventId, eventType, occurredAtUtc)
        };
    }

    private async Task<StripeCheckoutSessionResult> CreateCheckoutSessionAsync(
        IReadOnlyDictionary<string, string> values,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(_options.SecretKey))
        {
            throw new BillingCheckoutUnavailableException("Stripe checkout is not configured.");
        }

        using var request = new HttpRequestMessage(HttpMethod.Post, "v1/checkout/sessions");
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _options.SecretKey);
        request.Content = new FormUrlEncodedContent(values);

        using var response = await _httpClient.SendAsync(request, cancellationToken);
        var payload = await response.Content.ReadAsStringAsync(cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            throw new BillingCheckoutUnavailableException(
                $"Stripe checkout session creation failed with status {(int)response.StatusCode}. Body: {payload}");
        }

        using var document = JsonDocument.Parse(payload);
        var root = document.RootElement;
        var sessionId = root.GetProperty("id").GetString()
            ?? throw new BillingCheckoutUnavailableException("Stripe checkout response did not include a session id.");
        var url = root.GetProperty("url").GetString()
            ?? throw new BillingCheckoutUnavailableException("Stripe checkout response did not include a checkout URL.");

        return new StripeCheckoutSessionResult(
            sessionId,
            url,
            GetOptionalString(root, "customer"),
            GetOptionalString(root, "subscription"),
            GetOptionalString(root, "payment_intent"));
    }

    private static StripeCheckoutCompletedEvent ParseCheckoutCompletedEvent(
        string eventId,
        DateTime occurredAtUtc,
        JsonElement dataObject)
    {
        var metadata = GetOptionalObject(dataObject, "metadata");
        return new StripeCheckoutCompletedEvent(
            eventId,
            occurredAtUtc,
            dataObject.GetProperty("id").GetString() ?? throw new BillingWebhookValidationException("Stripe checkout session id is missing."),
            dataObject.GetProperty("mode").GetString() ?? string.Empty,
            dataObject.GetProperty("payment_status").GetString() ?? string.Empty,
            TryParseGuid(GetOptionalString(metadata, "workspace_id")),
            GetOptionalString(metadata, "billing_plan_code"),
            GetOptionalString(metadata, "credit_pack_code"),
            TryParseInt64(GetOptionalString(metadata, "credit_amount")),
            GetOptionalString(dataObject, "customer"),
            GetOptionalString(dataObject, "subscription"),
            GetOptionalString(dataObject, "payment_intent"));
    }

    private static StripeInvoicePaidEvent ParseInvoicePaidEvent(
        string eventId,
        DateTime occurredAtUtc,
        JsonElement dataObject)
    {
        var lines = dataObject.GetProperty("lines").GetProperty("data");
        var firstLine = lines.GetArrayLength() > 0
            ? lines[0]
            : throw new BillingWebhookValidationException("Stripe invoice event did not include invoice lines.");
        var period = firstLine.GetProperty("period");
        var price = firstLine.TryGetProperty("price", out var priceElement) ? priceElement : default;

        return new StripeInvoicePaidEvent(
            eventId,
            occurredAtUtc,
            dataObject.GetProperty("id").GetString() ?? throw new BillingWebhookValidationException("Stripe invoice id is missing."),
            dataObject.GetProperty("subscription").GetString() ?? throw new BillingWebhookValidationException("Stripe invoice subscription id is missing."),
            GetOptionalString(dataObject, "customer"),
            GetOptionalString(price, "id"),
            ConvertAmountFromStripeMinorUnits(dataObject.GetProperty("amount_paid").GetInt64()),
            (dataObject.GetProperty("currency").GetString() ?? "BRL").ToUpperInvariant(),
            DateTimeOffset.FromUnixTimeSeconds(period.GetProperty("start").GetInt64()).UtcDateTime,
            DateTimeOffset.FromUnixTimeSeconds(period.GetProperty("end").GetInt64()).UtcDateTime);
    }

    private static StripeSubscriptionUpdatedEvent ParseSubscriptionUpdatedEvent(
        string eventId,
        string eventType,
        DateTime occurredAtUtc,
        JsonElement dataObject)
    {
        var snapshot = ParseSubscriptionSnapshot(dataObject);

        return new StripeSubscriptionUpdatedEvent(
            eventId,
            eventType,
            occurredAtUtc,
            snapshot.SubscriptionId,
            snapshot.CustomerId,
            snapshot.PriceId,
            snapshot.Status,
            snapshot.CurrentPeriodStartUtc,
            snapshot.CurrentPeriodEndUtc,
            snapshot.CanceledAtUtc,
            snapshot.WorkspaceId,
            snapshot.PlanCode);
    }

    private static StripeSubscriptionSnapshot ParseSubscriptionSnapshot(JsonElement dataObject)
    {
        var items = dataObject.GetProperty("items").GetProperty("data");
        var firstItem = items.GetArrayLength() > 0 ? items[0] : default;
        var price = firstItem.ValueKind != JsonValueKind.Undefined && firstItem.TryGetProperty("price", out var priceElement)
            ? priceElement
            : default;
        var currentPeriodStartUtc = GetSubscriptionPeriodUtc(
            dataObject,
            firstItem,
            "current_period_start",
            "Stripe subscription period start is missing.");
        var currentPeriodEndUtc = GetSubscriptionPeriodUtc(
            dataObject,
            firstItem,
            "current_period_end",
            "Stripe subscription period end is missing.");
        var canceledAt = dataObject.TryGetProperty("canceled_at", out var canceledAtElement) &&
                         canceledAtElement.ValueKind != JsonValueKind.Null
            ? DateTimeOffset.FromUnixTimeSeconds(canceledAtElement.GetInt64()).UtcDateTime
            : (DateTime?)null;
        var metadata = GetOptionalObject(dataObject, "metadata");

        return new StripeSubscriptionSnapshot(
            dataObject.GetProperty("id").GetString() ?? throw new BillingWebhookValidationException("Stripe subscription id is missing."),
            TryParseGuid(GetOptionalString(metadata, "workspace_id")),
            GetOptionalString(metadata, "billing_plan_code"),
            GetOptionalString(dataObject, "customer"),
            GetOptionalString(price, "id"),
            dataObject.GetProperty("status").GetString() ?? string.Empty,
            currentPeriodStartUtc,
            currentPeriodEndUtc,
            canceledAt);
    }

    private static void ValidateSignature(
        string payload,
        string signatureHeader,
        string webhookSecret,
        int toleranceSeconds)
    {
        var headerSegments = signatureHeader
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        string? timestampValue = null;
        var signatures = new List<string>();

        foreach (var segment in headerSegments)
        {
            var parts = segment.Split('=', 2, StringSplitOptions.TrimEntries);
            if (parts.Length != 2)
            {
                continue;
            }

            if (string.Equals(parts[0], "t", StringComparison.OrdinalIgnoreCase))
            {
                timestampValue = parts[1];
            }
            else if (string.Equals(parts[0], "v1", StringComparison.OrdinalIgnoreCase))
            {
                signatures.Add(parts[1]);
            }
        }

        if (string.IsNullOrWhiteSpace(timestampValue) || signatures.Count == 0)
        {
            throw new BillingWebhookValidationException("Stripe webhook signature header is invalid.");
        }

        if (!long.TryParse(timestampValue, NumberStyles.Integer, CultureInfo.InvariantCulture, out var timestamp))
        {
            throw new BillingWebhookValidationException("Stripe webhook signature timestamp is invalid.");
        }

        var eventTimeUtc = DateTimeOffset.FromUnixTimeSeconds(timestamp);
        var age = DateTimeOffset.UtcNow - eventTimeUtc;
        if (age.Duration() > TimeSpan.FromSeconds(Math.Max(1, toleranceSeconds)))
        {
            throw new BillingWebhookValidationException("Stripe webhook signature timestamp is outside the allowed tolerance.");
        }

        var signedPayload = $"{timestampValue}.{payload}";

        // Stripe signing secrets are prefixed with "whsec_" followed by the base64-encoded key bytes.
        // The HMAC key must be the decoded bytes, NOT the raw UTF-8 string.
        var secretBase64 = webhookSecret.StartsWith("whsec_", StringComparison.OrdinalIgnoreCase)
            ? webhookSecret["whsec_".Length..]
            : webhookSecret;
        var secretBytes = Convert.FromBase64String(secretBase64);
        var payloadBytes = Encoding.UTF8.GetBytes(signedPayload);

        using var hmac = new HMACSHA256(secretBytes);
        var computedSignature = Convert.ToHexString(hmac.ComputeHash(payloadBytes)).ToLowerInvariant();
        var computedSignatureBytes = Encoding.UTF8.GetBytes(computedSignature);

        var matches = signatures.Any(signature =>
        {
            var signatureBytes = Encoding.UTF8.GetBytes(signature.Trim().ToLowerInvariant());
            return CryptographicOperations.FixedTimeEquals(signatureBytes, computedSignatureBytes);
        });

        if (!matches)
        {
            throw new BillingWebhookValidationException("Stripe webhook signature could not be verified.");
        }
    }

    private static JsonElement GetOptionalObject(JsonElement parent, string propertyName)
    {
        return parent.TryGetProperty(propertyName, out var value) ? value : default;
    }

    private static string? GetOptionalString(JsonElement parent, string propertyName)
    {
        if (parent.ValueKind == JsonValueKind.Undefined || !parent.TryGetProperty(propertyName, out var value))
        {
            return null;
        }

        return value.ValueKind == JsonValueKind.Null ? null : value.GetString();
    }

    private static DateTime GetSubscriptionPeriodUtc(
        JsonElement subscription,
        JsonElement firstItem,
        string propertyName,
        string errorMessage)
    {
        if (TryGetUnixTimeSeconds(subscription, propertyName, out var unixSeconds) ||
            TryGetUnixTimeSeconds(firstItem, propertyName, out unixSeconds))
        {
            return DateTimeOffset.FromUnixTimeSeconds(unixSeconds).UtcDateTime;
        }

        throw new BillingWebhookValidationException(errorMessage);
    }

    private static bool TryGetUnixTimeSeconds(JsonElement parent, string propertyName, out long unixSeconds)
    {
        unixSeconds = default;

        if (parent.ValueKind == JsonValueKind.Undefined ||
            !parent.TryGetProperty(propertyName, out var value) ||
            value.ValueKind == JsonValueKind.Null)
        {
            return false;
        }

        unixSeconds = value.GetInt64();
        return true;
    }

    private static Guid? TryParseGuid(string? value)
    {
        return Guid.TryParse(value, out var parsedValue) ? parsedValue : null;
    }

    private static long? TryParseInt64(string? value)
    {
        return long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsedValue)
            ? parsedValue
            : null;
    }

    private static decimal ConvertAmountFromStripeMinorUnits(long amountInMinorUnits)
    {
        return decimal.Round(amountInMinorUnits / 100m, 2, MidpointRounding.AwayFromZero);
    }
}
