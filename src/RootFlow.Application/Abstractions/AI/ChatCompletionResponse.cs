namespace RootFlow.Application.Abstractions.AI;

public sealed record ChatCompletionResponse(
    string Content,
    string? ModelName,
    string Provider,
    ChatCompletionUsage? Usage = null);
