namespace RootFlow.Application.Auth.Commands;

public sealed record SignupCommand(
    string FullName,
    string Email,
    string Password,
    string WorkspaceName);
