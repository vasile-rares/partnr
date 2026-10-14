using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
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

    public async Task<IReadOnlyList<Project>> ListSharedWithClientAsync(Guid userId, CancellationToken cancellationToken) =>
        await SharedWithClient(userId)
            .AsNoTracking()
            .Include(p => p.Stages)
            .OrderBy(p => p.DueDate == null)
            .ThenBy(p => p.DueDate)
            .ThenBy(p => p.Name)
            .ToListAsync(cancellationToken);

    public Task<Project?> GetSharedWithClientAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        SharedWithClient(userId).AsNoTracking().Include(p => p.Stages).FirstOrDefaultAsync(p => p.Id == id, cancellationToken);

    // The one definition of "a client may see this project", for the list and for a single project alike.
    private IQueryable<Project> SharedWithClient(Guid userId) =>
        dbContext.Projects.Where(p => p.ArchivedAtUtc == null
            && p.Status != ProjectStatus.Planning
            && dbContext.Clients.Any(c => c.Id == p.ClientId && c.UserId == userId && c.ArchivedAtUtc == null));

    public void Add(Project project) => dbContext.Projects.Add(project);
}
