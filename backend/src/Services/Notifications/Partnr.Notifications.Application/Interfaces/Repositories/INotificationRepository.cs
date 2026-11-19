using Partnr.Notifications.Domain.Entities;

namespace Partnr.Notifications.Application.Interfaces.Repositories;

public interface INotificationRepository
{
    /// <summary>The "approval requested" notification of this round, sent or not, as long as it still counts.</summary>
    Task<Notification?> GetApprovalRequestedAsync(Guid approvalRequestId, CancellationToken cancellationToken);

    /// <summary>Whether this notification is already here (obsolete or read as well): the same event delivered twice.</summary>
    Task<bool> ExistsAsync(string dedupeKey, CancellationToken cancellationToken);

    Task<bool> IsWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes up to <paramref name="limit"/> notifications whose email is due and pushes their next attempt forward by
    /// <paramref name="lease"/> in the same statement, so a second worker (or a crashed one, until the lease ends) cannot send the same email.
    /// </summary>
    Task<IReadOnlyList<Notification>> ClaimDueEmailsAsync(DateTime nowUtc, TimeSpan lease, int limit, CancellationToken cancellationToken);

    /// <summary>What the account's inbox shows, newest first: everything that is not obsolete.</summary>
    Task<IReadOnlyList<Notification>> ListInboxAsync(Guid userId, int limit, CancellationToken cancellationToken);

    Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Someone else's notification, an obsolete one and an unknown id are all null.</summary>
    Task<Notification?> GetInboxItemAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Notification notification);

    void AddWithdrawn(WithdrawnRequest withdrawn);
}
