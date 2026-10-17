using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

public interface IClientInvitationRepository
{
    Task<ClientInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>Neither accepted nor cancelled, expired or not. There is at most one.</summary>
    Task<ClientInvitation?> GetOpenForClientAsync(Guid clientId, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClientInvitation>> ListOpenForClientsAsync(IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken);

    void Add(ClientInvitation invitation);
}
