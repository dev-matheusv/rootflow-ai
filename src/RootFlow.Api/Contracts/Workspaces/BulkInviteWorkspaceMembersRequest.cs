namespace RootFlow.Api.Contracts.Workspaces;

public sealed record BulkInviteWorkspaceMembersRequest(IReadOnlyList<string>? Emails, string? Role);
