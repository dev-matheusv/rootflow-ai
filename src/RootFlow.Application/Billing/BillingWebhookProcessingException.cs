namespace RootFlow.Application.Billing;

public sealed class BillingWebhookProcessingException : Exception
{
    public BillingWebhookProcessingException(string message, Exception innerException)
        : base(message, innerException)
    {
    }
}
