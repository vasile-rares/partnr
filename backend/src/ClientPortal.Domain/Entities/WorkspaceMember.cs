using ClientPortal.Domain.Enums;

namespace ClientPortal.Domain.Entities;

/// <summary>Only changed through <see cref="Workspace"/>.</summary>
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
