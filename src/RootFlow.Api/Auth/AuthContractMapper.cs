using RootFlow.Api.Contracts.Auth;
using RootFlow.Application.Auth.Dtos;

namespace RootFlow.Api.Auth;

public static class AuthContractMapper
{
    public static SessionResponse ToResponse(this AuthSessionDto session)
    {
        return new SessionResponse(
            new AuthUserResponse(
                session.User.Id,
                session.User.FullName,
                session.User.Email),
            new AuthWorkspaceResponse(
                session.Workspace.Id,
                session.Workspace.Name,
                session.Workspace.Slug),
            session.Role.ToString());
    }

    public static AuthResponse ToResponse(this GeneratedJwtToken token, AuthSessionDto session)
    {
        return new AuthResponse(
            token.Token,
            token.ExpiresAtUtc,
            session.ToResponse());
    }
}
