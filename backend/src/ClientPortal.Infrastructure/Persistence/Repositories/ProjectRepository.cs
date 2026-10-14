using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ProjectRepository(ApplicationDbContext dbContext) : IProjectRepository
{
    public Task<Project?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Projects.Include(p => p.Stages).FirstOrDefaultAsync(p => p.WorkspaceId == workspaceId && p.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Project>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        Guid? clientId,
        CancellationToken cancellationToken) =>
        await dbContext.Projects
            .AsNoTracking()
            .Include(p => p.Stages)
            .Where(p => p.WorkspaceId == workspaceId
                && (includeArchived || p.ArchivedAtUtc == null)
                && (clientId == null || p.ClientId == clientId))
            .OrderBy(p => p.DueDate == null)
            .ThenBy(p => p.DueDate)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public void Add(Project project) => dbContext.Projects.Add(project);
}
