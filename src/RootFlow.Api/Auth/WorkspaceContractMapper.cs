using RootFlow.Api.Contracts.Workspaces;
using RootFlow.Application.Workspaces.Dtos;

namespace RootFlow.Api.Auth;

public static class WorkspaceContractMapper
{
    public static WorkspaceInvitationResponse ToResponse(this WorkspaceInvitationResultDto invitation)
    {
        return new WorkspaceInvitationResponse(
            invitation.Message,
            invitation.Email,
            invitation.Role.ToString(),
            invitation.ExpiresAtUtc);
    }

    public static WorkspaceMemberResponse ToResponse(this WorkspaceMemberDto member)
    {
        return new WorkspaceMemberResponse(
            member.UserId,
            member.FullName,
            member.Email,
            member.Role.ToString(),
            member.CreatedAtUtc,
            member.IsCurrentUser);
    }
}
