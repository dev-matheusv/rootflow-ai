namespace RootFlow.Application.Auth.Commands;

public sealed record ResetPasswordCommand(
    string Token,
    string NewPassword);
