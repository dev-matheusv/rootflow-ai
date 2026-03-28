namespace RootFlow.Application.Abstractions.AI;

public sealed record ChatCompletionRequest(IReadOnlyList<ChatPromptMessage> Messages);
