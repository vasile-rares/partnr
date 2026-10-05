namespace ClientPortal.Domain.Workspaces;

/// <summary>A user's membership in a workspace. Part of the <see cref="Workspace"/> aggregate: only changed through it.</summary>
public sealed class WorkspaceMember
{
    private WorkspaceMember()
    {
    }

    internal WorkspaceMember(Guid workspaceId, Guid userId, WorkspaceRole role, DateTime joinedAtUtc)
    {
        WorkspaceId = workspaceId;
        UserId = userId;
        Role = role;
        JoinedAtUtc = joinedAtUtc;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid UserId { get; private set; }

    public WorkspaceRole Role { get; private set; }

    public DateTime JoinedAtUtc { get; private set; }
}
