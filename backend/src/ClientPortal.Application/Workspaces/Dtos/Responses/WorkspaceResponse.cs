namespace ClientPortal.Application.Workspaces.Dtos.Responses;

/// <param name="Role">The signed-in user's role in this workspace: "Owner" or "Member".</param>
public sealed record WorkspaceResponse(Guid Id, string Name, string Role, DateTime CreatedAtUtc);
