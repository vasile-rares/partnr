using Partnr.Core.Application.DTOs.ProjectFiles;

namespace Partnr.Core.Application.Interfaces.Services;

public interface IProjectFileService
{
    Task<ProjectFileResponse> CreateAsync(
        Guid workspaceId,
        Guid projectId,
        UploadProjectFileRequest request,
        FileUpload file,
        CancellationToken cancellationToken);

    Task<IReadOnlyList<ProjectFileResponse>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken);

    Task<ProjectFileResponse> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    /// <summary>Open to a client too, for a shared, non-archived file of a project shared with them; anything else is NotFound for them.</summary>
    Task<FileDownload> DownloadAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectFileResponse> UpdateAsync(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveProjectFileRequest request,
        CancellationToken cancellationToken);

    Task<ProjectFileResponse> ShareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectFileResponse> UnshareAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectFileResponse> ArchiveAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task<ProjectFileResponse> RestoreAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    Task DeleteAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);
}
