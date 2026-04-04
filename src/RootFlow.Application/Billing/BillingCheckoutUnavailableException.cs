namespace RootFlow.Application.Billing;

public sealed class BillingCheckoutUnavailableException : Exception
{
    public BillingCheckoutUnavailableException(string message)
        : base(message)
    {
    }
}
