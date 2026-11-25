using Partnr.Contracts.Events;
using Partnr.Notifications.Application.Interfaces.Services;

namespace Partnr.Notifications.Api.Events;

// What the other services say happened. Wolverine finds these by convention; delivery is at least once and the service takes it in that way.
public static class ApprovalRequestedHandler
{
    public static Task Handle(ApprovalRequested message, INotificationEventService events, CancellationToken cancellationToken) =>
        events.ApplyAsync(message, cancellationToken);
}

public static class ApprovalDecidedHandler
{
    public static Task Handle(ApprovalDecided message, INotificationEventService events, CancellationToken cancellationToken) =>
        events.ApplyAsync(message, cancellationToken);
}

public static class InvitationAcceptedHandler
{
    public static Task Handle(InvitationAccepted message, INotificationEventService events, CancellationToken cancellationToken) =>
        events.ApplyAsync(message, cancellationToken);
}

public static class MemberJoinedHandler
{
    public static Task Handle(MemberJoined message, INotificationEventService events, CancellationToken cancellationToken) =>
        events.ApplyAsync(message, cancellationToken);
}

public static class ApprovalRequestWithdrawnHandler
{
    public static Task Handle(ApprovalRequestWithdrawn message, INotificationEventService events, CancellationToken cancellationToken) =>
        events.WithdrawAsync(message.ApprovalRequestId, cancellationToken);
}

public static class ConversationChangedHandler
{
    public static Task Handle(ConversationChanged message, INotificationEventService events, CancellationToken cancellationToken) =>
        events.ApplyAsync(message, cancellationToken);
}

public static class ClientInvitedHandler
{
    public static Task Handle(ClientInvited message, ITransactionalEmailService emails, CancellationToken cancellationToken) =>
        emails.SendAsync(message, cancellationToken);
}

public static class MemberInvitedHandler
{
    public static Task Handle(MemberInvited message, ITransactionalEmailService emails, CancellationToken cancellationToken) =>
        emails.SendAsync(message, cancellationToken);
}

public static class PasswordResetRequestedHandler
{
    public static Task Handle(PasswordResetRequested message, ITransactionalEmailService emails, CancellationToken cancellationToken) =>
        emails.SendAsync(message, cancellationToken);
}
