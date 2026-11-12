using Partnr.Application.DTOs.Email;

namespace Partnr.Application.Interfaces.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
