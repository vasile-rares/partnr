using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ClientRepository(ApplicationDbContext dbContext) : IClientRepository
{
    public Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Clients.FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Id == id, cancellationToken);

    public Task<bool> EmailExistsAsync(Guid workspaceId, string email, CancellationToken cancellationToken) =>
        dbContext.Clients.AnyAsync(c => c.WorkspaceId == workspaceId && c.Email == email, cancellationToken);

    public void Add(Client client) => dbContext.Clients.Add(client);
}
