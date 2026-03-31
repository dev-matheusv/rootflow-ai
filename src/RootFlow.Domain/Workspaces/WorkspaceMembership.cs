namespace RootFlow.Domain.Workspaces;

public sealed class WorkspaceMembership
{
    private WorkspaceMembership()
    {
    }

    public WorkspaceMembership(
        Guid id,
        Guid workspaceId,
        Guid userId,
        WorkspaceRole role,
        DateTime createdAtUtc)
    {
        if (id == Guid.Empty)
        {
            throw new ArgumentException("Membership id cannot be empty.", nameof(id));
        }

        if (workspaceId == Guid.Empty)
        {
            throw new ArgumentException("Workspace id cannot be empty.", nameof(workspaceId));
        }

        if (userId == Guid.Empty)
        {
            throw new ArgumentException("User id cannot be empty.", nameof(userId));
        }

        Id = id;
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        CreatedAtUtc = createdAtUtc;
        IsActive = true;
    }

    public Guid Id { get; private set; }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public bool IsActive { get; private set; }

    public void ChangeRole(WorkspaceRole role)
    {
        Role = role;
    }

    public void Activate()
    {
        IsActive = true;
    }

    public void Deactivate()
    {
        IsActive = false;
    }
}
