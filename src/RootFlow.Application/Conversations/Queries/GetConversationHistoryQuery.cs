namespace RootFlow.Application.Conversations.Queries;

public sealed record GetConversationHistoryQuery(Guid WorkspaceId, Guid ConversationId, Guid UserId, bool IsWorkspaceAdmin);
