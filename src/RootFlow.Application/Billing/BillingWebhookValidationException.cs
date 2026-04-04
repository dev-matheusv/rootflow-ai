namespace RootFlow.Application.Billing;

public sealed class BillingWebhookValidationException : Exception
{
    public BillingWebhookValidationException(string message)
        : base(message)
    {
    }
}
