namespace ClientPortal.Application.DTOs.Workspaces;

/// <param name="Role">The signed-in user's role in this workspace: "Owner" or "Member".</param>
public sealed record WorkspaceResponse(Guid Id, string Name, string Role, DateTime CreatedAtUtc);
