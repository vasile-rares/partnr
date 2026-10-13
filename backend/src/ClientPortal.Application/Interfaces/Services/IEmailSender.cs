using ClientPortal.Application.DTOs.Email;

namespace ClientPortal.Application.Interfaces.Services;

/// <summary>
/// Sends email. Implemented over SMTP in Infrastructure; in development the SMTP server is Mailpit, which catches every
/// message instead of delivering it.
/// </summary>
public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
