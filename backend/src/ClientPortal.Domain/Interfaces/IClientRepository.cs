using ClientPortal.Domain.Entities;

namespace ClientPortal.Domain.Interfaces;

public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken);

    void Add(Client client);
}
