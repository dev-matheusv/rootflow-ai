namespace RootFlow.Application.Billing.Commands;

public sealed record RegisterWorkspaceUsageCommand(
    Guid WorkspaceId,
    Guid? UserId,
    Guid? ConversationId,
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens = 0);
