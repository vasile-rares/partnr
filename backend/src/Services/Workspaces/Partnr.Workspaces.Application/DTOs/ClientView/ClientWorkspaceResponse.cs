namespace Partnr.Workspaces.Application.DTOs.ClientView;

/// <summary>Only what a client may see: no roles, no members, no other clients.</summary>
public sealed record ClientWorkspaceResponse(Guid Id, string Name);
