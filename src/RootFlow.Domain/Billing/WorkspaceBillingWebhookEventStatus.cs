namespace RootFlow.Domain.Billing;

public enum WorkspaceBillingWebhookEventStatus
{
    Pending = 1,
    Processing = 2,
    Processed = 3,
    Failed = 4
}
