using Partnr.Application.DTOs;
using Partnr.Application.DTOs.ClientView;
using Partnr.Application.DTOs.Projects;

namespace Partnr.Application.Interfaces.Services;

public interface IProjectService
{
    Task<ProjectResponse> CreateAsync(Guid workspaceId, CreateProjectRequest request, CancellationToken cancellationToken);

    /// <summary>Open to a client too: they get the projects shared with them, in their own shape; the filters are the firm's and ignored for them.</summary>
    Task<RoleResponse<IReadOnlyList<ProjectResponse>, IReadOnlyList<ClientProjectSummaryResponse>>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        Guid? clientId,
        CancellationToken cancellationToken);

    /// <summary>Open to a client too, for a project shared with them in this firm; a draft, an archived project, someone else's and an unknown id all answer the same NotFound.</summary>
    Task<RoleResponse<ProjectResponse, ClientProjectResponse>> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

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
