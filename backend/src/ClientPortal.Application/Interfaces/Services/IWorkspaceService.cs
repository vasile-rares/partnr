using ClientPortal.Application.DTOs.Workspaces;

namespace ClientPortal.Application.Interfaces.Services;

public interface IWorkspaceService
{
    Task<WorkspaceResponse> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<WorkspaceResponse>> ListMineAsync(CancellationToken cancellationToken);

    Task<WorkspaceResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    Task<WorkspaceResponse> RenameAsync(Guid id, RenameWorkspaceRequest request, CancellationToken cancellationToken);
}
