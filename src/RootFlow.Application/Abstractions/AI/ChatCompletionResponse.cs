namespace RootFlow.Application.Abstractions.AI;

public sealed record ChatCompletionResponse(string Content, string? ModelName);
