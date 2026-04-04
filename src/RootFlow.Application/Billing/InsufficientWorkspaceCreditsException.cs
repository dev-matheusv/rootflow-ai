namespace RootFlow.Application.Billing;

public sealed class InsufficientWorkspaceCreditsException : Exception
{
    public InsufficientWorkspaceCreditsException(string message)
        : base(message)
    {
    }
}
