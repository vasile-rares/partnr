using Partnr.Notifications.Application.DTOs.Notifications;
using Partnr.Notifications.Application.Notifications;
using Partnr.Notifications.Domain.Entities;

namespace Partnr.Notifications.Application.Mappings;

internal static class NotificationMappings
{
    public static NotificationResponse ToResponse(this Notification notification)
    {
        var content = NotificationContent.For(notification);

        return new NotificationResponse(
            notification.Id,
            content.Subject,
            content.Headline,
            content.Path,
            notification.WorkspaceName,
            notification.CreatedAtUtc,
            notification.IsRead);
    }
}
