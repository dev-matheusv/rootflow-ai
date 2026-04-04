namespace RootFlow.Application.Abstractions.Auth;

public interface IPlatformAdminAccessService
{
    bool HasAccess(string? email);
}
