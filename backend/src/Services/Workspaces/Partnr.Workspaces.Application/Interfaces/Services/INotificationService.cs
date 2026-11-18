using Partnr.Workspaces.Application.DTOs.Notifications;

namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>The signed-in account's own inbox, across every workspace it belongs to.</summary>
public interface INotificationService
{
    /// <summary>The 30 newest, plus how many are unread in all.</summary>
    Task<NotificationsResponse> ListAsync(CancellationToken cancellationToken);

    Task<UnreadCountResponse> UnreadCountAsync(CancellationToken cancellationToken);

    /// <summary>Someone else's notification, an obsolete one and an unknown id are all a 404.</summary>
    Task MarkReadAsync(Guid id, CancellationToken cancellationToken);

    Task MarkAllReadAsync(CancellationToken cancellationToken);
}
