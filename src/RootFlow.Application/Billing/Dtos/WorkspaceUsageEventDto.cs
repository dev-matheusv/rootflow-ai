namespace RootFlow.Application.Billing.Dtos;

public sealed record WorkspaceUsageEventDto(
    Guid Id,
    Guid WorkspaceId,
    Guid? UserId,
    Guid? ConversationId,
    string Provider,
    string Model,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal EstimatedCost,
    long CreditsCharged,
    DateTime CreatedAtUtc);
