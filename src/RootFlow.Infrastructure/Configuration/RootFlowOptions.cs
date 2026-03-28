namespace RootFlow.Infrastructure.Configuration;

public sealed class RootFlowOptions
{
    public Guid DefaultWorkspaceId { get; set; } = Guid.Parse("11111111-1111-1111-1111-111111111111");

    public string DefaultWorkspaceName { get; set; } = "RootFlow Default Workspace";

    public string DefaultWorkspaceSlug { get; set; } = "default";
}
