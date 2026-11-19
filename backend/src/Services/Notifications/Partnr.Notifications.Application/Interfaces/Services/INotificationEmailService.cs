namespace Partnr.Notifications.Application.Interfaces.Services;

public interface INotificationEmailService
{
    /// <summary>Sends the emails that are due. A failure of one never stops the others; it is kept on the notification and retried later.</summary>
    Task<NotificationEmailRun> SendDueAsync(CancellationToken cancellationToken);
}

public sealed record NotificationEmailRun(int Sent, int Failed);
