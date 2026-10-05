using ClientPortal.Application.DTOs.Workspaces;

namespace ClientPortal.Application.Interfaces.Services;

public interface IWorkspaceService
{
    /// <summary>Creates a workspace owned by the signed-in user.</summary>
    Task<WorkspaceResponse> CreateAsync(CreateWorkspaceRequest request, CancellationToken cancellationToken);

    /// <summary>The workspaces the signed-in user is a member of.</summary>
    Task<IReadOnlyList<WorkspaceResponse>> ListMineAsync(CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The workspace does not exist or the user is not a member.</exception>
    Task<WorkspaceResponse> GetAsync(Guid id, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The workspace does not exist or the user is not a member.</exception>
    /// <exception cref="Common.Exceptions.ForbiddenException">The user is a member but not the owner.</exception>
    Task<WorkspaceResponse> RenameAsync(Guid id, RenameWorkspaceRequest request, CancellationToken cancellationToken);
}
