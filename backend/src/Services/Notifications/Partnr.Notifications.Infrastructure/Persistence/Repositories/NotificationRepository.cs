using Partnr.Notifications.Application.Interfaces.Repositories;
using Partnr.Notifications.Domain.Entities;
using Partnr.Notifications.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Notifications.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(NotificationsDbContext dbContext) : INotificationRepository
{
    public Task<Notification?> GetApprovalRequestedAsync(Guid approvalRequestId, CancellationToken cancellationToken) =>
        dbContext.Notifications.FirstOrDefaultAsync(
            n => n.ApprovalRequestId == approvalRequestId
                && n.Type == NotificationType.ApprovalRequested
                && n.ObsoleteAtUtc == null,
            cancellationToken);

    public Task<bool> ExistsAsync(string dedupeKey, CancellationToken cancellationToken) =>
        dbContext.Notifications.AnyAsync(n => n.DedupeKey == dedupeKey, cancellationToken);

    public Task<bool> IsWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken) =>
        dbContext.WithdrawnRequests.AnyAsync(w => w.Id == approvalRequestId, cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListInboxAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        await dbContext.Notifications
            .AsNoTracking()
            .Where(n => n.RecipientUserId == userId && n.ObsoleteAtUtc == null)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        dbContext.Notifications.CountAsync(
            n => n.RecipientUserId == userId && n.ObsoleteAtUtc == null && n.ReadAtUtc == null,
            cancellationToken);

    public Task<Notification?> GetInboxItemAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        dbContext.Notifications.FirstOrDefaultAsync(
            n => n.Id == id && n.RecipientUserId == userId && n.ObsoleteAtUtc == null,
            cancellationToken);

    public async Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        await dbContext.Notifications
            .Where(n => n.RecipientUserId == userId && n.ObsoleteAtUtc == null && n.ReadAtUtc == null)
            .ToListAsync(cancellationToken);

    // One statement: the rows are chosen and their next attempt pushed out together, and SKIP LOCKED lets two workers each take different rows.
    public async Task<IReadOnlyList<Notification>> ClaimDueEmailsAsync(
        DateTime nowUtc,
        TimeSpan lease,
        int limit,
        CancellationToken cancellationToken) =>
        await dbContext.Notifications
            .FromSqlInterpolated($"""
                UPDATE notifications SET "EmailNextAttemptAtUtc" = {nowUtc + lease}
                WHERE "Id" IN (
                    SELECT "Id" FROM notifications
                    WHERE "EmailStatus" = 'Pending' AND "EmailNextAttemptAtUtc" <= {nowUtc}
                    ORDER BY "EmailNextAttemptAtUtc"
                    LIMIT {limit}
                    FOR UPDATE SKIP LOCKED)
                RETURNING *
                """)
            .ToListAsync(cancellationToken);

    public void Add(Notification notification) => dbContext.Notifications.Add(notification);

    public void AddWithdrawn(WithdrawnRequest withdrawn) => dbContext.WithdrawnRequests.Add(withdrawn);
}
