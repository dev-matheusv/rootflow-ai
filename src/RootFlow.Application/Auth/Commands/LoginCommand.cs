namespace RootFlow.Application.Auth.Commands;

public sealed record LoginCommand(
    string Email,
    string Password);
