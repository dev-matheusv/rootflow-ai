namespace RootFlow.Application.Abstractions.Auth;

public interface IAppLinkBuilder
{
    string BuildPasswordResetLink(string token, bool requireAbsoluteUrl = false);

    string BuildWorkspaceInviteLink(string token, bool requireAbsoluteUrl = false);

    string BuildAppRouteLink(string routePathWithQuery, bool requireAbsoluteUrl = false);
}
