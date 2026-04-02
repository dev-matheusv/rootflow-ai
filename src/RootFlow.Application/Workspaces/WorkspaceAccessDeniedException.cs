namespace RootFlow.Application.Workspaces;

public sealed class WorkspaceAccessDeniedException : Exception
{
    public WorkspaceAccessDeniedException(string message) : base(message)
    {
    }
}
