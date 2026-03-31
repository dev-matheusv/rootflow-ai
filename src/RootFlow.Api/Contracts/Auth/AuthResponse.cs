namespace RootFlow.Api.Contracts.Auth;

public sealed record AuthUserResponse(
    Guid Id,
    string FullName,
    string Email);

public sealed record AuthWorkspaceResponse(
    Guid Id,
    string Name,
    string Slug);

public sealed record SessionResponse(
    AuthUserResponse User,
    AuthWorkspaceResponse Workspace,
    string Role);

public sealed record AuthResponse(
    string Token,
    DateTime ExpiresAtUtc,
    SessionResponse Session);
