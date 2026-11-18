using Partnr.Workspaces.Application.DTOs.Notifications;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Application.Mappings;

namespace Partnr.Workspaces.Application.Services;

internal sealed class NotificationService(
    INotificationRepository notifications,
    ICurrentUser currentUser,
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
        var notification = await notifications.GetInboxItemAsync(RequireUserId(), id, cancellationToken)
            ?? throw new NotFoundException($"The notification with id '{id}' was not found.");

        notification.MarkRead(Now());
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task MarkAllReadAsync(CancellationToken cancellationToken)
    {
        var now = Now();
        foreach (var notification in await notifications.ListUnreadAsync(RequireUserId(), cancellationToken))
        {
            notification.MarkRead(now);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private Guid RequireUserId() => currentUser.UserId ?? throw new UnauthorizedException("Not signed in.");

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
