using Partnr.Application.Interfaces.Repositories;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace Partnr.Infrastructure.Persistence.Repositories;

internal sealed class NotificationRepository(ApplicationDbContext dbContext) : INotificationRepository
{
    public Task<Notification?> GetApprovalRequestedAsync(Guid approvalRequestId, CancellationToken cancellationToken) =>
        dbContext.Notifications.FirstOrDefaultAsync(
            n => n.ApprovalRequestId == approvalRequestId
                && n.Type == NotificationType.ApprovalRequested
                && n.ObsoleteAtUtc == null,
            cancellationToken);

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
}
