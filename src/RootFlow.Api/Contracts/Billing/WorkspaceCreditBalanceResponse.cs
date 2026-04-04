namespace RootFlow.Api.Contracts.Billing;

public sealed record WorkspaceCreditBalanceResponse(
    Guid WorkspaceId,
    long AvailableCredits,
    long ConsumedCredits,
    DateTime UpdatedAtUtc);
