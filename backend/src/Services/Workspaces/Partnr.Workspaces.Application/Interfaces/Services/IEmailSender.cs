using Partnr.Workspaces.Application.DTOs.Email;

namespace Partnr.Workspaces.Application.Interfaces.Services;

public interface IEmailSender
{
    Task SendAsync(EmailMessage message, CancellationToken cancellationToken);
}
