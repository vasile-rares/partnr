using ClientPortal.Application.DTOs.Portal;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>The portal, from the client's side: what the signed-in user is a client of.</summary>
public interface IPortalService
{
    /// <summary>
    /// The workspaces whose client card is linked to the signed-in account, oldest first. A client the workspace archived is
    /// left out until it is restored, so archiving also takes the portal access away.
    /// </summary>
    /// <exception cref="Common.Exceptions.UnauthorizedException">Nobody is signed in.</exception>
    Task<IReadOnlyList<PortalWorkspaceResponse>> ListWorkspacesAsync(CancellationToken cancellationToken);

    /// <summary>
    /// The projects shared with the signed-in client, across all their workspaces, soonest due first. A project is shared once it
    /// leaves Planning; archived projects, and projects of an archived client, are left out.
    /// </summary>
    /// <exception cref="Common.Exceptions.UnauthorizedException">Nobody is signed in.</exception>
    Task<IReadOnlyList<PortalProjectSummaryResponse>> ListProjectsAsync(CancellationToken cancellationToken);

    /// <summary>One shared project with its stages.</summary>
    /// <exception cref="Common.Exceptions.UnauthorizedException">Nobody is signed in.</exception>
    /// <exception cref="Common.Exceptions.NotFoundException">
    /// There is no such project, it is not shared with this account (a draft, archived, or someone else's), or it belongs to no
    /// workspace the account is a client of. All look the same.
    /// </exception>
    Task<PortalProjectResponse> GetProjectAsync(Guid id, CancellationToken cancellationToken);
}
