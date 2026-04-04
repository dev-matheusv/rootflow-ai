namespace RootFlow.Application.Billing.Dtos;

public sealed record WorkspaceCreditBalanceDto(
    Guid WorkspaceId,
    long AvailableCredits,
    long ConsumedCredits,
    DateTime UpdatedAtUtc);
