using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ClientRepository(ApplicationDbContext dbContext) : IClientRepository
{
    public Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Clients.FirstOrDefaultAsync(c => c.WorkspaceId == workspaceId && c.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Client>> ListAsync(Guid workspaceId, bool includeArchived, CancellationToken cancellationToken) =>
        await dbContext.Clients
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId && (includeArchived || c.ArchivedAtUtc == null))
            .OrderBy(c => c.Name)
            .ThenBy(c => c.Email)
            .ToListAsync(cancellationToken);

    public Task<bool> EmailExistsAsync(Guid workspaceId, string email, Guid? exceptClientId, CancellationToken cancellationToken) =>
        dbContext.Clients.AnyAsync(
            c => c.WorkspaceId == workspaceId && c.Email == email && c.Id != exceptClientId,
            cancellationToken);

    public Task<bool> IsLinkedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Clients.AnyAsync(c => c.WorkspaceId == workspaceId && c.UserId == userId, cancellationToken);

    public void Add(Client client) => dbContext.Clients.Add(client);
}
