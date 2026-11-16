using Partnr.Application.Interfaces.Repositories;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Infrastructure.Persistence.Repositories;

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

    // One statement: the rows are chosen and their next try pushed out together, and SKIP LOCKED lets two workers each take different rows.
    public async Task<IReadOnlyList<Meeting>> ClaimDueForSyncAsync(DateTime nowUtc, TimeSpan lease, int limit, CancellationToken cancellationToken) =>
        await dbContext.Meetings
            .FromSqlInterpolated($"""
                UPDATE meetings SET "NextSyncAtUtc" = {nowUtc + lease}
                WHERE "Id" IN (
                    SELECT "Id" FROM meetings
                    WHERE "SyncStatus" = 'Pending' AND "NextSyncAtUtc" <= {nowUtc}
                    ORDER BY "NextSyncAtUtc"
                    LIMIT {limit}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<Meeting>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await dbContext.Meetings
            .Where(m => m.OrganizerUserId == organizerUserId && m.EndsAtUtc > nowUtc && m.SyncStatus != MeetingSyncStatus.Synced)
            .ToListAsync(cancellationToken);

    public void Add(Meeting meeting) => dbContext.Meetings.Add(meeting);
}
