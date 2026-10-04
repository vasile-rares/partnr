using ClientPortal.Domain.Clients;

namespace ClientPortal.Application.Clients;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    /// <summary>Tracks the client; it is persisted by <see cref="Common.IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Client client);
}
