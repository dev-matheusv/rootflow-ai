namespace RootFlow.Application.Auth;

public sealed class AuthConflictException : InvalidOperationException
{
    public AuthConflictException(string message)
        : base(message)
    {
    }
}
