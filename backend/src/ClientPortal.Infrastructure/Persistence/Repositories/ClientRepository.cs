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
            .OrderBy(c => c.Name ?? c.Email)
            .ThenBy(c => c.Email)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Client>> ListByIdsAsync(Guid workspaceId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken)
    {
        var wanted = ids.ToArray();
        return await dbContext.Clients
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId && wanted.Contains(c.Id))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> EmailExistsAsync(Guid workspaceId, string email, Guid? exceptClientId, CancellationToken cancellationToken) =>
        dbContext.Clients.AnyAsync(
            c => c.WorkspaceId == workspaceId && c.Email == email && c.Id != exceptClientId,
            cancellationToken);

    public async Task<IReadOnlySet<string>> ExistingEmailsAsync(Guid workspaceId, IReadOnlyCollection<string> emails, CancellationToken cancellationToken)
    {
        var wanted = emails.ToArray();
        var found = await dbContext.Clients
            .AsNoTracking()
            .Where(c => c.WorkspaceId == workspaceId && wanted.Contains(c.Email))
            .Select(c => c.Email)
            .ToListAsync(cancellationToken);

        return found.ToHashSet();
    }

    public Task<bool> IsLinkedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken) =>
        dbContext.Clients.AnyAsync(c => c.WorkspaceId == workspaceId && c.UserId == userId, cancellationToken);

    public void Add(Client client) => dbContext.Clients.Add(client);
}
