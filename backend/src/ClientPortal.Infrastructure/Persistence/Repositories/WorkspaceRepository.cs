using ClientPortal.Application.Workspaces;
using ClientPortal.Domain.Workspaces;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class WorkspaceRepository(ApplicationDbContext dbContext) : IWorkspaceRepository
{
    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Workspaces.Include(w => w.Members).FirstOrDefaultAsync(w => w.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Workspaces
            .Include(w => w.Members)
            .Where(w => w.Members.Any(m => m.UserId == userId))
            .OrderBy(w => w.CreatedAtUtc)
            .ToListAsync(cancellationToken);

    public void Add(Workspace workspace) => dbContext.Workspaces.Add(workspace);
}
