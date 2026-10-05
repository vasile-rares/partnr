namespace ClientPortal.Domain.Workspaces;

/// <summary>What a member may do in a workspace. Clients are not members: they get access through invitations.</summary>
public enum WorkspaceRole
{
    /// <summary>Created the workspace; manages its settings and, later, its members.</summary>
    Owner,

    /// <summary>Works on the workspace's clients and projects.</summary>
    Member
}
