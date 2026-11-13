namespace RootFlow.Application.Workspaces;

public sealed class WorkspaceMemberNotFoundException : Exception
{
    public WorkspaceMemberNotFoundException(string message) : base(message)
    {
    }
}
