using ClientPortal.Application.Clients;
using ClientPortal.Domain.Clients;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ClientRepository(ApplicationDbContext dbContext) : IClientRepository
{
    public Task<Client?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Clients.FirstOrDefaultAsync(c => c.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(string email, CancellationToken cancellationToken) =>
        dbContext.Clients.AnyAsync(c => c.Email == email, cancellationToken);

    public void Add(Client client) => dbContext.Clients.Add(client);
}
