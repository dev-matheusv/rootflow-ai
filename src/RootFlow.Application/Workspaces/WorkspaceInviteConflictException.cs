namespace RootFlow.Application.Workspaces;

public sealed class WorkspaceInviteConflictException : Exception
{
    public WorkspaceInviteConflictException(string message) : base(message)
    {
    }
}
