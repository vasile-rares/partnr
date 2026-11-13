using System.Net;
using Partnr.Application.DTOs.Auth;
using Partnr.Application.DTOs.Email;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Notifications;
using Partnr.Domain.Entities;

namespace Partnr.Application.Services;

internal sealed class NotificationEmailService(
    INotificationRepository notifications,
    IIdentityService identity,
    IEmailSender email,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    TimeProvider time) : INotificationEmailService
{
    private const int BatchSize = 20;

    // Longer than any send takes; only matters if a worker dies halfway, when the row comes back after it.
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    public async Task<NotificationEmailRun> SendDueAsync(CancellationToken cancellationToken)
    {
        var due = await notifications.ClaimDueEmailsAsync(Now(), Lease, BatchSize, cancellationToken);
        var sent = 0;
        var failed = 0;

        foreach (var notification in due)
        {
            try
            {
                var account = await identity.FindByIdAsync(notification.RecipientUserId, cancellationToken);
                if (account is null)
                {
                    notification.CancelEmail();
                }
                else
                {
                    await email.SendAsync(BuildEmail(notification, account), cancellationToken);
                    notification.MarkEmailSent(Now());
                    sent++;
                }
            }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                notification.RecordEmailFailure(Now(), ex.Message);
                failed++;
            }

            // One save per email: a later failure must not forget that this one already left.
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new NotificationEmailRun(sent, failed);
    }

    private EmailMessage BuildEmail(Notification notification, CurrentUserResponse recipient)
    {
        var content = NotificationContent.For(notification);
        var link = links.Url(content.Path);
        var greeting = string.IsNullOrWhiteSpace(recipient.FullName) ? "Hi," : $"Hi {recipient.FullName},";

        var text = $"""
            {greeting}

            {content.Headline}

            {content.ButtonLabel}:
            {link}
            """;

        var html = $"""
            <p>{WebUtility.HtmlEncode(greeting)}</p>
            <p>{WebUtility.HtmlEncode(content.Headline)}</p>
            <p><a href="{WebUtility.HtmlEncode(link)}" style="display:inline-block;padding:10px 16px;background:#111827;color:#ffffff;text-decoration:none;border-radius:6px">{WebUtility.HtmlEncode(content.ButtonLabel)}</a></p>
            """;

        return new EmailMessage(recipient.Email, content.Subject, text, html);
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
