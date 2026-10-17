namespace ClientPortal.Domain.Enums;

/// <summary>What a member may do in a workspace. Clients are not members: they get access through invitations.</summary>
public enum WorkspaceRole
{
    Owner,

    Member
}
