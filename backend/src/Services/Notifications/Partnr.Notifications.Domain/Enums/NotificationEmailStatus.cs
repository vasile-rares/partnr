namespace Partnr.Notifications.Domain.Enums;

/// <summary>Where the email of a notification stands. Only Pending is ever picked up again.</summary>
public enum NotificationEmailStatus
{
    Pending,

    Sent,

    /// <summary>Not sent on purpose: the request it was about was taken back, or the recipient is gone.</summary>
    Cancelled,

    /// <summary>Every attempt failed.</summary>
    Failed
}
