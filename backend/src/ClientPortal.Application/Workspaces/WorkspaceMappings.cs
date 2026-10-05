using ClientPortal.Application.Workspaces.Dtos.Responses;
using ClientPortal.Domain.Workspaces;

namespace ClientPortal.Application.Workspaces;

internal static class WorkspaceMappings
{
    public static WorkspaceResponse ToResponse(this Workspace workspace, WorkspaceRole role) =>
        new(workspace.Id, workspace.Name, role.ToString(), workspace.CreatedAtUtc);
}
