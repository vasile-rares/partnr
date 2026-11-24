namespace Partnr.Core.Application.DTOs.Workspaces;

/// <param name="Role">The signed-in user's role in this workspace: "Owner" or "Member", or "Client" for someone who only follows their projects in it.</param>
public sealed record WorkspaceResponse(Guid Id, string Name, string Role, DateTime CreatedAtUtc);
