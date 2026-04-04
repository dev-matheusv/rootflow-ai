namespace RootFlow.Application.Abstractions.Billing;

public sealed record AiUsageCharge(
    decimal EstimatedCost,
    long CreditsCharged);
