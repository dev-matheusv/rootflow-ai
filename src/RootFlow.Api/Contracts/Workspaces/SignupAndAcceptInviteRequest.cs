namespace RootFlow.Api.Contracts.Workspaces;

public sealed record SignupAndAcceptInviteRequest(
    string FullName,
    string Password,
    string Token);
