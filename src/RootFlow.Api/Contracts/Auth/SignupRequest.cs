namespace RootFlow.Api.Contracts.Auth;

public sealed record SignupRequest(
    string FullName,
    string Email,
    string Password,
    string WorkspaceName);
