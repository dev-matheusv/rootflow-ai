namespace RootFlow.Application.Abstractions.AI;

public sealed record ChatCompletionUsage(
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens);
