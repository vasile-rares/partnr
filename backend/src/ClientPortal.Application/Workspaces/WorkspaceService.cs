using ClientPortal.Application.Common;
using ClientPortal.Application.Workspaces.Dtos.Requests;
using ClientPortal.Application.Workspaces.Dtos.Responses;
using ClientPortal.Domain.Workspaces;

namespace ClientPortal.Application.Workspaces;

internal sealed class WorkspaceService(
    IWorkspaceRepository workspaces,
    WorkspaceAccess access,
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

        return mine.Select(w => w.ToResponse(w.RoleOf(userId)!.Value)).ToList();
    }

    public async Task<WorkspaceResponse> GetAsync(Guid id, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(id, cancellationToken);

        return membership.Workspace.ToResponse(membership.Role);
    }

    public async Task<WorkspaceResponse> RenameAsync(Guid id, RenameWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var membership = await access.RequireOwnerAsync(id, cancellationToken);

        membership.Workspace.Rename(request.Name);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return membership.Workspace.ToResponse(membership.Role);
    }
}
