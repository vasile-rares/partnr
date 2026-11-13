using Partnr.Application.DTOs.Notifications;
using Partnr.Application.Notifications;
using Partnr.Domain.Entities;

namespace Partnr.Application.Mappings;

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
