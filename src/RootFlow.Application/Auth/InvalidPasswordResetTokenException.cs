namespace RootFlow.Application.Auth;

public sealed class InvalidPasswordResetTokenException : Exception
{
    public InvalidPasswordResetTokenException(string message)
        : base(message)
    {
    }
}
