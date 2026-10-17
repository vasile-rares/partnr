using ClientPortal.Application.DTOs.Projects;

namespace ClientPortal.Application.Interfaces.Services;

public interface IProjectService
{
    Task<ProjectResponse> CreateAsync(Guid workspaceId, CreateProjectRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectResponse>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        Guid? clientId,
        CancellationToken cancellationToken);

    Task<ProjectResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    Task<ProjectResponse> UpdateAsync(Guid workspaceId, Guid id, UpdateProjectRequest request, CancellationToken cancellationToken);

    Task<ProjectResponse> AddStageAsync(Guid workspaceId, Guid projectId, CreateStageRequest request, CancellationToken cancellationToken);

    Task<ProjectResponse> UpdateStageAsync(
        Guid workspaceId,
        Guid projectId,
        Guid stageId,
        UpdateStageRequest request,
        CancellationToken cancellationToken);

    Task<ProjectResponse> MoveStageAsync(
        Guid workspaceId,
        Guid projectId,
        Guid stageId,
        MoveStageRequest request,
        CancellationToken cancellationToken);

    Task<ProjectResponse> RemoveStageAsync(Guid workspaceId, Guid projectId, Guid stageId, CancellationToken cancellationToken);

    Task<ProjectResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    Task<ProjectResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);
}
