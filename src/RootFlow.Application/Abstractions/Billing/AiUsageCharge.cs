namespace RootFlow.Application.Abstractions.Billing;

public sealed record AiUsageCharge(
    decimal EstimatedCost,
    decimal ChargedCost,
    long CreditsCharged);
