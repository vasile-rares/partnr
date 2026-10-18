using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ProjectUpdateRepository(ApplicationDbContext dbContext) : IProjectUpdateRepository
{
    public Task<ProjectUpdate?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        dbContext.ProjectUpdates.FirstOrDefaultAsync(
            u => u.WorkspaceId == workspaceId && u.ProjectId == projectId && u.Id == id,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectUpdate>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectUpdates
            .AsNoTracking()
            .Where(u => u.WorkspaceId == workspaceId && u.ProjectId == projectId && (includeArchived || u.ArchivedAtUtc == null))
            .OrderByDescending(u => u.CreatedAtUtc)
            .ThenByDescending(u => u.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectUpdate>> ListSharedAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectUpdates
            .AsNoTracking()
            .Where(u => u.ProjectId == projectId && u.Status == ProjectUpdateStatus.Shared && u.ArchivedAtUtc == null)
            .OrderByDescending(u => u.SharedAtUtc)
            .ThenByDescending(u => u.Id)
            .ToListAsync(cancellationToken);

    public void Add(ProjectUpdate update) => dbContext.ProjectUpdates.Add(update);

    public void Remove(ProjectUpdate update) => dbContext.ProjectUpdates.Remove(update);
}
