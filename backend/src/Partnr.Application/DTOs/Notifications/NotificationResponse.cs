namespace Partnr.Application.DTOs.Notifications;

/// <param name="Title">One short line, the same as the email's subject.</param>
/// <param name="Text">The sentence under it.</param>
/// <param name="Path">Where it leads, inside the web app (starts with a slash).</param>
public sealed record NotificationResponse(
    Guid Id,
    string Title,
    string Text,
    string Path,
    string WorkspaceName,
    DateTime CreatedAtUtc,
    bool IsRead);

/// <param name="Unread">Counts every unread one, not only those in <paramref name="Items"/>.</param>
public sealed record NotificationsResponse(int Unread, IReadOnlyList<NotificationResponse> Items);

public sealed record UnreadCountResponse(int Unread);
