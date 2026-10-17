namespace ClientPortal.Application.DTOs.Portal;

/// <summary>Only what a client may see: no roles, no members, no other clients.</summary>
public sealed record PortalWorkspaceResponse(Guid Id, string Name);
