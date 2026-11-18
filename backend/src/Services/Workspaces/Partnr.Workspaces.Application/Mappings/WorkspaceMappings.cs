using Partnr.Workspaces.Application.DTOs.Workspaces;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Mappings;

internal static class WorkspaceMappings
{
    public static WorkspaceResponse ToResponse(this Workspace workspace, WorkspaceRole role) =>
        new(workspace.Id, workspace.Name, role.ToString(), workspace.CreatedAtUtc);

    /// <summary>A client of the workspace is not a member, so it has no <see cref="WorkspaceRole"/>.</summary>
    public static WorkspaceResponse ToClientResponse(this Workspace workspace) =>
        new(workspace.Id, workspace.Name, "Client", workspace.CreatedAtUtc);
}
