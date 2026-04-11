namespace RootFlow.Application.Workspaces.Commands;

public sealed record SignupAndAcceptInviteCommand(
    string FullName,
    string Password,
    string InviteToken);
