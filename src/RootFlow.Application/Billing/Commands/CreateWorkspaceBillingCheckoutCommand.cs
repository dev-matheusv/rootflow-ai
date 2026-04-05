namespace RootFlow.Application.Billing.Commands;

public sealed record CreateWorkspaceBillingCheckoutCommand(
    Guid WorkspaceId,
    string PriceId);
