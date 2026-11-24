using Partnr.Core.Application.Interfaces.Repositories;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Core.Infrastructure.Persistence.Repositories;

internal sealed class MeetingRepository(ApplicationDbContext dbContext) : IMeetingRepository
{
    public Task<Meeting?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Meetings.FirstOrDefaultAsync(m => m.WorkspaceId == workspaceId && m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Meeting>> ListAsync(
        Guid workspaceId,
        bool includeCancelled,
        Guid? clientId,
        Guid? projectId,
        CancellationToken cancellationToken) =>
        await dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.WorkspaceId == workspaceId
                && (includeCancelled || m.Status != MeetingStatus.Cancelled)
                && (clientId == null || m.ClientId == clientId)
                && (projectId == null || m.ProjectId == projectId))
            .OrderBy(m => m.StartsAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Meeting>> ListForClientAccountAsync(
        Guid workspaceId,
        Guid userId,
        DateTime since,
        CancellationToken cancellationToken) =>
        await dbContext.Meetings
            .AsNoTracking()
            .Where(m => m.WorkspaceId == workspaceId
                && m.StartsAtUtc >= since
                && dbContext.Clients.Any(c => c.Id == m.ClientId && c.UserId == userId && c.ArchivedAtUtc == null))
            .OrderBy(m => m.StartsAtUtc)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public Task<Meeting?> FindAsync(Guid id, CancellationToken cancellationToken) =>
        dbContext.Meetings.FirstOrDefaultAsync(m => m.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Meeting>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await dbContext.Meetings
            .Where(m => m.OrganizerUserId == organizerUserId && m.EndsAtUtc > nowUtc && m.SyncStatus != MeetingSyncStatus.Synced)
            .ToListAsync(cancellationToken);

    public void Add(Meeting meeting) => dbContext.Meetings.Add(meeting);
}
