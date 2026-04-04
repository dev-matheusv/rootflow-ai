namespace RootFlow.Domain.Billing;

public sealed class WorkspaceUsageEvent
{
    private WorkspaceUsageEvent()
    {
    }

    public WorkspaceUsageEvent(
        Guid id,
        Guid workspaceId,
        Guid? userId,
        Guid? conversationId,
        string provider,
        string model,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        decimal estimatedCost,
        long creditsCharged,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Workspace usage event id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (userId.HasValue && userId.Value == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty when it is provided.", nameof(userId));
        }

        if (conversationId.HasValue && conversationId.Value == Guid.Empty)
        {
            throw new ArgumentException("Conversation id cannot be empty when it is provided.", nameof(conversationId));
        }

        if (promptTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(promptTokens), "Prompt tokens cannot be negative.");
        }

        if (completionTokens < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(completionTokens), "Completion tokens cannot be negative.");
        }

        if (estimatedCost < 0m)
        {
            throw new ArgumentOutOfRangeException(nameof(estimatedCost), "Estimated cost cannot be negative.");
        }

        if (creditsCharged < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(creditsCharged), "Credits charged cannot be negative.");
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(provider);
        ArgumentException.ThrowIfNullOrWhiteSpace(model);

        var normalizedTotalTokens = totalTokens == 0
            ? checked(promptTokens + completionTokens)
            : totalTokens;

        if (normalizedTotalTokens < promptTokens + completionTokens)
        {
            throw new ArgumentOutOfRangeException(nameof(totalTokens), "Total tokens cannot be less than the prompt and completion token sum.");
        }

        Id = id;
        WorkspaceId = workspaceId;
        UserId = userId;
        ConversationId = conversationId;
        Provider = provider.Trim().ToLowerInvariant();
        Model = model.Trim();
        PromptTokens = promptTokens;
        CompletionTokens = completionTokens;
        TotalTokens = normalizedTotalTokens;
        EstimatedCost = decimal.Round(estimatedCost, 6, MidpointRounding.AwayFromZero);
        CreditsCharged = creditsCharged;
        CreatedAtUtc = createdAtUtc;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid? UserId { get; private set; }

    public Guid? ConversationId { get; private set; }

    public string Provider { get; private set; } = null!;

    public string Model { get; private set; } = null!;

    public int PromptTokens { get; private set; }

    public int CompletionTokens { get; private set; }

    public int TotalTokens { get; private set; }

    public decimal EstimatedCost { get; private set; }

    public long CreditsCharged { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }
}
