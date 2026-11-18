using Partnr.Workspaces.Application.DTOs.Notifications;
using Partnr.Workspaces.Application.Notifications;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Mappings;

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
