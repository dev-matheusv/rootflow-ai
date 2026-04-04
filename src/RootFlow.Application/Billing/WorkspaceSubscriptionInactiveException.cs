namespace RootFlow.Application.Billing;

public sealed class WorkspaceSubscriptionInactiveException : Exception
{
    public WorkspaceSubscriptionInactiveException(string message)
        : base(message)
    {
    }
}
