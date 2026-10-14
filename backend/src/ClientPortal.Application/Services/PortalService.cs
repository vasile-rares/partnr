using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;

namespace ClientPortal.Application.Services;

internal sealed class PortalService(IWorkspaceRepository workspaces, WorkspaceAccess access) : IPortalService
{
    public async Task<IReadOnlyList<PortalWorkspaceResponse>> ListWorkspacesAsync(CancellationToken cancellationToken)
    {
        var list = await workspaces.ListForClientAsync(access.RequireUserId(), cancellationToken);

        return list.Select(w => new PortalWorkspaceResponse(w.Id, w.Name)).ToList();
    }
}
