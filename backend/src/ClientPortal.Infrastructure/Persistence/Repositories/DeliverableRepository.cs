using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class DeliverableRepository(ApplicationDbContext dbContext) : IDeliverableRepository
{
    public Task<Deliverable?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Deliverables.FirstOrDefaultAsync(
            d => d.WorkspaceId == workspaceId && d.ProjectId == projectId && d.Id == id,
            cancellationToken);

    public async Task<IReadOnlyList<Deliverable>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken) =>
        await dbContext.Deliverables
            .AsNoTracking()
            .Where(d => d.WorkspaceId == workspaceId && d.ProjectId == projectId && (includeArchived || d.ArchivedAtUtc == null))
            .OrderByDescending(d => d.CreatedAtUtc)
            .ThenByDescending(d => d.Id)
            .ToListAsync(cancellationToken);

    public void Add(Deliverable deliverable) => dbContext.Deliverables.Add(deliverable);

    public void Remove(Deliverable deliverable) => dbContext.Deliverables.Remove(deliverable);
}
