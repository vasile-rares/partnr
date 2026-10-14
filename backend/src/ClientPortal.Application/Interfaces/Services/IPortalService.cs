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
}
