using ClientPortal.Application.DTOs.ProjectUpdates;

namespace ClientPortal.Application.Interfaces.Services;

public interface IProjectUpdateService
{
    Task<ProjectUpdateResponse> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        SaveProjectUpdateRequest request,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectUpdateResponse>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<ProjectUpdateResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectUpdateResponse> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveProjectUpdateRequest request,
        CancellationToken cancellationToken);

    Task<ProjectUpdateResponse> ShareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectUpdateResponse> UnshareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectUpdateResponse> ArchiveAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectUpdateResponse> RestoreAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);
}
