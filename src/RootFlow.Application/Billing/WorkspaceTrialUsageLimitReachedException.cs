namespace RootFlow.Application.Billing;

public sealed class WorkspaceTrialUsageLimitReachedException : Exception
{
    public WorkspaceTrialUsageLimitReachedException(string message)
        : base(message)
    {
    }
}
