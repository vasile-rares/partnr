using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

public interface IClientInvitationRepository
{
    /// <summary>The invitation whose token has this hash, or null.</summary>
    Task<ClientInvitation?> GetByTokenHashAsync(string tokenHash, CancellationToken cancellationToken);

    /// <summary>The client's open invitation (neither accepted nor cancelled, expired or not), or null. There is at most one.</summary>
    Task<ClientInvitation?> GetOpenForClientAsync(Guid clientId, CancellationToken cancellationToken);

    /// <summary>The open invitations of these clients, without tracking (for display).</summary>
    Task<IReadOnlyList<ClientInvitation>> ListOpenForClientsAsync(IReadOnlyCollection<Guid> clientIds, CancellationToken cancellationToken);

    /// <summary>Tracks the invitation; it is persisted by <see cref="Common.IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(ClientInvitation invitation);
}
