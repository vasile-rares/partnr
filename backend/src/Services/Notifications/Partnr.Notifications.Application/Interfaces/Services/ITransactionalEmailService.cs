using Partnr.Contracts.Events;

namespace Partnr.Notifications.Application.Interfaces.Services;

/// <summary>
/// The emails that carry a secret link (an invitation, a password reset). Unlike a notification they are not stored: the broker keeps
/// the event and retries it until the send works, so nothing here keeps the token longer than that.
/// </summary>
public interface ITransactionalEmailService
{
    Task SendAsync(ClientInvited invited, CancellationToken cancellationToken);

    Task SendAsync(MemberInvited invited, CancellationToken cancellationToken);

    Task SendAsync(PasswordResetRequested requested, CancellationToken cancellationToken);
}
