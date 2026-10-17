using ClientPortal.Application.DTOs.Clients;

namespace ClientPortal.Application.Interfaces.Services;

public interface IClientInvitationService
{
    /// <summary>Replaces any open invitation, so only the newest link works.</summary>
    Task<ClientResponse> InviteAsync(Guid workspaceId, Guid clientId, CancellationToken cancellationToken);

    /// <summary>Nothing to cancel is not an error.</summary>
    Task<ClientResponse> CancelAsync(Guid workspaceId, Guid clientId, CancellationToken cancellationToken);
}
