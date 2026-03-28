using RootFlow.Domain.Conversations;

namespace RootFlow.Application.Abstractions.AI;

public sealed record ChatPromptMessage(MessageRole Role, string Content);
