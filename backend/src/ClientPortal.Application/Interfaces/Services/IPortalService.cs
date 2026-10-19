using ClientPortal.Application.DTOs.Portal;
using ClientPortal.Application.DTOs.ProjectFiles;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>The portal, from the client's side: what the signed-in user is a client of.</summary>
public interface IPortalService
{
    /// <summary>Workspaces whose client card is linked to the signed-in account. Archived clients are left out.</summary>
    Task<IReadOnlyList<PortalWorkspaceResponse>> ListWorkspacesAsync(CancellationToken cancellationToken);

    /// <summary>Projects shared with the signed-in client: out of Planning and not archived, soonest due first.</summary>
    Task<IReadOnlyList<PortalProjectSummaryResponse>> ListProjectsAsync(CancellationToken cancellationToken);

    /// <summary>A draft, an archived project, someone else's and an unknown id all answer the same NotFound.</summary>
    Task<PortalProjectResponse> GetProjectAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The same door as <see cref="GetProjectAsync"/>, and the file must be shared and not archived; anything else is NotFound.</summary>
    Task<FileDownload> DownloadFileAsync(Guid projectId, Guid fileId, CancellationToken cancellationToken);
}
