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

    /// <exception cref="Exceptions.NotFoundException">The workspace or the project does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ForbiddenException">The user is a member but not the owner.</exception>
    Task<ProjectResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <exception cref="Exceptions.NotFoundException">The workspace or the project does not exist, or the user is not a member.</exception>
    /// <exception cref="Exceptions.ForbiddenException">The user is a member but not the owner.</exception>
    Task<ProjectResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);
}
