using ClientPortal.Application.DTOs.Projects;

namespace ClientPortal.Application.Interfaces.Services;

public interface IProjectService
{
    /// <exception cref="Exceptions.NotFoundException">The workspace or the client does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ConflictException">The client is archived.</exception>
    Task<ProjectResponse> CreateAsync(Guid workspaceId, CreateProjectRequest request, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace does not exist or the user is not a member.</exception>
    Task<IReadOnlyList<ProjectResponse>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        Guid? clientId,
        CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace or the project does not exist, or the user is not a member.</exception>
    Task<ProjectResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace or the project does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ConflictException">The project is archived.</exception>
    Task<ProjectResponse> UpdateAsync(Guid workspaceId, Guid id, UpdateProjectRequest request, CancellationToken cancellationToken);

    /// <summary>Adds a stage to the end of the project and returns the whole project.</summary>
    /// <exception cref="Exceptions.NotFoundException">The workspace or the project does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ConflictException">The project is archived.</exception>
    Task<ProjectResponse> AddStageAsync(Guid workspaceId, Guid projectId, CreateStageRequest request, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace, the project or the stage does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ConflictException">The project is archived.</exception>
    Task<ProjectResponse> UpdateStageAsync(
        Guid workspaceId,
        Guid projectId,
        Guid stageId,
        UpdateStageRequest request,
        CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace, the project or the stage does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ConflictException">The project is archived.</exception>
    Task<ProjectResponse> MoveStageAsync(
        Guid workspaceId,
        Guid projectId,
        Guid stageId,
        MoveStageRequest request,
        CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace, the project or the stage does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ConflictException">The project is archived.</exception>
    Task<ProjectResponse> RemoveStageAsync(Guid workspaceId, Guid projectId, Guid stageId, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace or the project does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ForbiddenException">The user is a member but not the owner.</exception>
    Task<ProjectResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace or the project does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ForbiddenException">The user is a member but not the owner.</exception>
    Task<ProjectResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);
}
