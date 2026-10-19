using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace ClientPortal.Infrastructure.Persistence.Repositories;

internal sealed class ProjectFileRepository(ApplicationDbContext dbContext) : IProjectFileRepository
{
    public Task<ProjectFile?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        dbContext.ProjectFiles.FirstOrDefaultAsync(
            f => f.WorkspaceId == workspaceId && f.ProjectId == projectId && f.Id == id,
            cancellationToken);

    public async Task<IReadOnlyList<ProjectFile>> ListAsync(
        Guid workspaceId,
        Guid projectId,
        bool includeArchived,
        CancellationToken cancellationToken) =>
        await dbContext.ProjectFiles
            .AsNoTracking()
            .Where(f => f.WorkspaceId == workspaceId && f.ProjectId == projectId && (includeArchived || f.ArchivedAtUtc == null))
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenByDescending(f => f.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<ProjectFile>> ListSharedAsync(Guid projectId, CancellationToken cancellationToken) =>
        await dbContext.ProjectFiles
            .AsNoTracking()
            .Where(f => f.ProjectId == projectId && f.Status == ProjectFileStatus.Shared && f.ArchivedAtUtc == null)
            .OrderByDescending(f => f.CreatedAtUtc)
            .ThenByDescending(f => f.Id)
            .ToListAsync(cancellationToken);

    public void Add(ProjectFile file) => dbContext.ProjectFiles.Add(file);

    public void Remove(ProjectFile file) => dbContext.ProjectFiles.Remove(file);
}
