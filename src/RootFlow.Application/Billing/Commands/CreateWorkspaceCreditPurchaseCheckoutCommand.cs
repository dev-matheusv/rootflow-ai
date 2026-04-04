namespace RootFlow.Application.Billing.Commands;

public sealed record CreateWorkspaceCreditPurchaseCheckoutCommand(
    Guid WorkspaceId,
    string CreditPackCode);
