using Partnr.Application.DTOs.Workspaces;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Mappings;

internal static class WorkspaceMappings
{
    public static WorkspaceResponse ToResponse(this Workspace workspace, WorkspaceRole role) =>
        new(workspace.Id, workspace.Name, role.ToString(), workspace.CreatedAtUtc);
}
