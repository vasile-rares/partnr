using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.DTOs.Workspaces;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Application.Mappings;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Services;

internal sealed class WorkspaceService(
    IWorkspaceRepository workspaces,
    WorkspaceAccess access,
    ClientAccess clientAccess,
    CallerAccess callers,
    IUnitOfWork unitOfWork) : IWorkspaceService
{
    public async Task<WorkspaceResponse> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var workspace = Workspace.Create(request.Name, access.RequireUserId());
        workspaces.Add(workspace);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return workspace.ToResponse(WorkspaceRole.Owner);
    }

    public async Task<IReadOnlyList<WorkspaceResponse>> ListMineAsync(CancellationToken cancellationToken)
    {
        var userId = access.RequireUserId();
        var mine = await workspaces.ListForUserAsync(userId, cancellationToken);
        var asClient = await clientAccess.ListWorkspacesAsync(userId, cancellationToken);

        // An account is never both in the same workspace, so nothing shows twice.
        return mine.Select(w => w.ToResponse(w.RoleOf(userId)!.Value)).Concat(asClient.Select(w => w.ToClientResponse())).ToList();
    }

    public async Task<WorkspaceResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var caller = await callers.RequireAsync(id, cancellationToken);

        return caller.Role is { } role ? caller.Workspace.ToResponse(role) : caller.Workspace.ToClientResponse();
    }

    public async Task<WorkspaceResponse> RenameAsync(Guid id, RenameWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var membership = await access.RequireOwnerAsync(id, cancellationToken);

        membership.Workspace.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return membership.Workspace.ToResponse(membership.Role);
    }
}
