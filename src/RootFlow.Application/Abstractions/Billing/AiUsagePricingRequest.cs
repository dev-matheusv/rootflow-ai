namespace RootFlow.Application.Abstractions.Billing;

public sealed record AiUsagePricingRequest(
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);
