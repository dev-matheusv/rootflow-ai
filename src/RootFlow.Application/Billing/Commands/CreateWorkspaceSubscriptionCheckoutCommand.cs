namespace RootFlow.Application.Billing.Commands;

public sealed record CreateWorkspaceSubscriptionCheckoutCommand(
    Guid WorkspaceId,
    string PlanCode);
