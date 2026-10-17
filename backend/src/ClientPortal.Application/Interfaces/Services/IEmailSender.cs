using ClientPortal.Application.DTOs.Email;

namespace ClientPortal.Application.Interfaces.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
