namespace RootFlow.Domain.Billing;

public sealed class WorkspaceBillingWebhookEvent
{
    private const int MaxErrorLength = 2_000;

    private WorkspaceBillingWebhookEvent()
    {
    }

    public WorkspaceBillingWebhookEvent(
        Guid id,
        string provider,
        string providerEventId,
        string eventType,
        string payload,
        string signatureHeader,
        WorkspaceBillingWebhookEventStatus status,
        int attemptCount,
        DateTime firstReceivedAtUtc,
        DateTime lastReceivedAtUtc,
        DateTime updatedAtUtc,
        DateTime? processingStartedAtUtc = null,
        DateTime? processedAtUtc = null,
        string? lastError = null)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Billing webhook event id cannot be empty.", nameof(id));
        }

        if (attemptCount < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(attemptCount), "Attempt count cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(providerEventId);
        ArgumentException.ThrowIfNullOrWhiteSpace(eventType);
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureHeader);

        Id = id;
        Provider = provider.Trim().ToLowerInvariant();
        ProviderEventId = providerEventId.Trim();
        EventType = eventType.Trim();
        Payload = payload;
        SignatureHeader = signatureHeader.Trim();
        Status = status;
        AttemptCount = attemptCount;
        FirstReceivedAtUtc = firstReceivedAtUtc;
        LastReceivedAtUtc = lastReceivedAtUtc;
        UpdatedAtUtc = updatedAtUtc;
        ProcessingStartedAtUtc = processingStartedAtUtc;
        ProcessedAtUtc = processedAtUtc;
        LastError = NormalizeError(lastError);
    }

    public Guid Id { get; private set; }

    public string Provider { get; private set; } = null!;

    public string ProviderEventId { get; private set; } = null!;

    public string EventType { get; private set; } = null!;

    public string Payload { get; private set; } = null!;

    public string SignatureHeader { get; private set; } = null!;

    public WorkspaceBillingWebhookEventStatus Status { get; private set; }

    public int AttemptCount { get; private set; }

    public DateTime FirstReceivedAtUtc { get; private set; }

    public DateTime LastReceivedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public DateTime? ProcessingStartedAtUtc { get; private set; }

    public DateTime? ProcessedAtUtc { get; private set; }

    public string? LastError { get; private set; }

    public void RecordReceipt(string payload, string signatureHeader, DateTime receivedAtUtc)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(payload);
        ArgumentException.ThrowIfNullOrWhiteSpace(signatureHeader);

        Payload = payload;
        SignatureHeader = signatureHeader.Trim();
        LastReceivedAtUtc = receivedAtUtc;
        UpdatedAtUtc = receivedAtUtc;
    }

    public bool TryMarkProcessing(DateTime startedAtUtc)
    {
        if (Status == WorkspaceBillingWebhookEventStatus.Processed ||
            Status == WorkspaceBillingWebhookEventStatus.Processing)
        {
            return false;
        }

        Status = WorkspaceBillingWebhookEventStatus.Processing;
        AttemptCount += 1;
        ProcessingStartedAtUtc = startedAtUtc;
        UpdatedAtUtc = startedAtUtc;
        LastError = null;
        return true;
    }

    public void MarkProcessed(DateTime processedAtUtc)
    {
        Status = WorkspaceBillingWebhookEventStatus.Processed;
        ProcessingStartedAtUtc = null;
        ProcessedAtUtc = processedAtUtc;
        UpdatedAtUtc = processedAtUtc;
        LastError = null;
    }

    public void MarkFailed(string error, DateTime failedAtUtc)
    {
        Status = WorkspaceBillingWebhookEventStatus.Failed;
        ProcessingStartedAtUtc = null;
        UpdatedAtUtc = failedAtUtc;
        LastError = NormalizeError(error);
    }

    private static string? NormalizeError(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        return trimmed.Length <= MaxErrorLength
            ? trimmed
            : trimmed[..MaxErrorLength];
    }
}
