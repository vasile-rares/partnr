using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Mappings;

internal static class WorkspaceMappings
{
    public static WorkspaceResponse ToResponse(this Workspace workspace, WorkspaceRole role) =>
        new(workspace.Id, workspace.Name, role.ToString(), workspace.CreatedAtUtc);
}
