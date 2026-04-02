namespace RootFlow.Application.Workspaces;

public sealed class InvalidWorkspaceInvitationException : Exception
{
    public InvalidWorkspaceInvitationException(string message) : base(message)
    {
    }
}
