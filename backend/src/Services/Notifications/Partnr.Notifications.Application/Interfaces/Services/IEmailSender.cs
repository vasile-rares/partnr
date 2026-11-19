using Partnr.Notifications.Application.DTOs.Email;

namespace Partnr.Notifications.Application.Interfaces.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
