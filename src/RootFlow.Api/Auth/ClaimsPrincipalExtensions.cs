using System.Security.Claims;

namespace RootFlow.Api.Auth;

public static class ClaimsPrincipalExtensions
{
    public static Guid GetRequiredUserId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(ClaimTypes.NameIdentifier);
        if (!Guid.TryParse(value, out var userId))
        {
            throw new InvalidOperationException("Authenticated user id claim is missing or invalid.");
        }

        return userId;
    }

    public static Guid GetRequiredWorkspaceId(this ClaimsPrincipal principal)
    {
        var value = principal.FindFirstValue(RootFlowClaimTypes.WorkspaceId);
        if (!Guid.TryParse(value, out var workspaceId))
        {
            throw new InvalidOperationException("Authenticated workspace id claim is missing or invalid.");
        }

        return workspaceId;
    }
}
