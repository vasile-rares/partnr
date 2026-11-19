using Partnr.Notifications.Application.DTOs.Notifications;
using Partnr.Notifications.Application.Exceptions;
using Partnr.Notifications.Application.Interfaces.Repositories;
using Partnr.Notifications.Application.Interfaces.Services;
using Partnr.Notifications.Application.Mappings;

namespace Partnr.Notifications.Application.Services;

internal sealed class NotificationService(
    INotificationRepository notifications,
    ICurrentUser currentUser,
    IInboxPublisher inbox,
    IUnitOfWork unitOfWork,
    TimeProvider time) : INotificationService
{
    private const int PageSize = 30;

    public async Task<NotificationsResponse> ListAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var items = await notifications.ListInboxAsync(userId, PageSize, cancellationToken);
        var unread = await notifications.CountUnreadAsync(userId, cancellationToken);

        return new NotificationsResponse(unread, items.Select(n => n.ToResponse()).ToList());
    }

    public async Task<UnreadCountResponse> UnreadCountAsync(CancellationToken cancellationToken) =>
        new(await notifications.CountUnreadAsync(RequireUserId(), cancellationToken));

    public async Task MarkReadAsync(Guid id, CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var notification = await notifications.GetInboxItemAsync(userId, id, cancellationToken)
            ?? throw new NotFoundException($"The notification with id '{id}' was not found.");

        notification.MarkRead(Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // The account's other tabs and devices show the number too.
        await inbox.InboxChangedAsync([userId], cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var userId = RequireUserId();
        var now = Now();
        var unread = await notifications.ListUnreadAsync(userId, cancellationToken);
        foreach (var notification in unread)
        {
            notification.MarkRead(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (unread.Count > 0)
        {
            await inbox.InboxChangedAsync([userId], cancellationToken);
        }
    }

    private Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Not signed in.");

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
