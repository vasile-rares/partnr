using Microsoft.EntityFrameworkCore;
using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.Infrastructure.Persistence.Repositories;

internal sealed class MeetingSyncRepository(IntegrationsDbContext dbContext) : IMeetingSyncRepository
{
    public Task<MeetingSync?> GetByIdAsync(Guid meetingId, CancellationToken cancellationToken) =>
        dbContext.MeetingSyncs.FirstOrDefaultAsync(m => m.Id == meetingId, cancellationToken);

    // One statement: the rows are chosen and their next try pushed out together, and SKIP LOCKED lets two workers each take different rows.
    public async Task<IReadOnlyList<MeetingSync>> ClaimDueAsync(DateTime nowUtc, TimeSpan lease, int limit, CancellationToken cancellationToken) =>
        await dbContext.MeetingSyncs
            .FromSqlInterpolated($"""
                UPDATE meeting_syncs SET "NextSyncAtUtc" = {nowUtc + lease}
                WHERE "Id" IN (
                    SELECT "Id" FROM meeting_syncs
                    WHERE "Status" = 'Pending' AND "NextSyncAtUtc" <= {nowUtc}
                    ORDER BY "NextSyncAtUtc"
                    LIMIT {limit}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .AsNoTracking()
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<MeetingSync>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
        await dbContext.MeetingSyncs
            .Where(m => m.OrganizerUserId == organizerUserId && m.EndsAtUtc > nowUtc && m.Status != MeetingSyncStatus.Synced)
            .ToListAsync(cancellationToken);

    public void Add(MeetingSync meetingSync) => dbContext.MeetingSyncs.Add(meetingSync);
}
