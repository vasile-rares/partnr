using Partnr.Contracts.Events;

namespace Partnr.Notifications.Application.Interfaces.Services;

/// <summary>
/// What the events of other services do here. Events are delivered at least once and kinds of events travel on different queues, so
/// every step is safe to repeat and to arrive in any order.
/// </summary>
public interface INotificationEventService
{
    Task ApplyAsync(ApprovalRequested requested, CancellationToken cancellationToken);

    Task ApplyAsync(ApprovalDecided decided, CancellationToken cancellationToken);

    Task ApplyAsync(InvitationAccepted accepted, CancellationToken cancellationToken);

    /// <summary>The request was taken back: its notification leaves the inbox and its email, if it has not gone out yet, is cancelled.</summary>
    Task WithdrawAsync(Guid approvalRequestId, CancellationToken cancellationToken);

    /// <summary>Only a live push: the browsers of these accounts reload the conversation.</summary>
    Task ApplyAsync(ConversationChanged changed, CancellationToken cancellationToken);
}
