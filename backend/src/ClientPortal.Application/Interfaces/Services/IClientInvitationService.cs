using ClientPortal.Application.DTOs.Clients;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>Inviting a client to the portal, from the workspace's side.</summary>
public interface IClientInvitationService
{
    /// <summary>Sends an invitation by email. If one is already open it is cancelled and replaced, so only the newest link works.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The workspace or the client does not exist, or the user is not a member.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The client is archived or already has an account.</exception>
    Task<ClientResponse> InviteAsync(Guid workspaceId, Guid clientId, CancellationToken cancellationToken);

    /// <summary>Withdraws the open invitation, which also unlocks the client's details. Nothing to cancel is not an error.</summary>
    /// <exception cref="Common.Exceptions.NotFoundException">The workspace or the client does not exist, or the user is not a member.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The client already accepted, so there is nothing to withdraw.</exception>
    Task<ClientResponse> CancelAsync(Guid workspaceId, Guid clientId, CancellationToken cancellationToken);
}
