using Partnr.Notifications.Application.DTOs.Email;
using Partnr.Notifications.Application.Interfaces.Repositories;
using Partnr.Notifications.Application.Interfaces.Services;
using Partnr.Notifications.Domain.Entities;
using Partnr.Notifications.Domain.Enums;

namespace Partnr.Notifications.UnitTests.Application;

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.CompletedTask;
    }
}

internal sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; set; } = userId;
}

internal sealed class FakeTimeProvider : TimeProvider
{
    public DateTimeOffset Now { get; set; } = new(2026, 10, 14, 9, 0, 0, TimeSpan.Zero);

    public override DateTimeOffset GetUtcNow() => Now;
}

internal sealed class FakeAppLinks : IAppLinks
{
    public string Url(string path) => $"https://app.test{path}";
}

internal sealed class FakeInboxPublisher : IInboxPublisher
{
    public List<IReadOnlyCollection<Guid>> Pushes { get; } = [];

    public Task InboxChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        Pushes.Add(userIds);
        return Task.CompletedTask;
    }
}

internal sealed class FakeConversationPublisher : IConversationPublisher
{
    public List<(IReadOnlyCollection<Guid> UserIds, Guid ProjectId)> Pushes { get; } = [];

    public Task ConversationChangedAsync(IReadOnlyCollection<Guid> userIds, Guid projectId, CancellationToken cancellationToken)
    {
        Pushes.Add((userIds, projectId));
        return Task.CompletedTask;
    }
}

internal sealed class FakeEmailSender : IEmailSender
{
    public List<EmailMessage> Sent { get; } = [];

    /// <summary>Thrown by the next sends until it is cleared.</summary>
    public Exception? Throw { get; set; }

    public Task SendAsync(EmailMessage message, CancellationToken cancellationToken)
    {
        if (Throw is not null)
        {
            throw Throw;
        }

        Sent.Add(message);
        return Task.CompletedTask;
    }
}

internal sealed class FakeNotificationRepository : INotificationRepository
{
    public List<Notification> Items { get; } = [];

    public HashSet<Guid> Withdrawn { get; } = [];

    public Task<Notification?> GetApprovalRequestedAsync(Guid approvalRequestId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(n =>
            n.ApprovalRequestId == approvalRequestId && n.Type == NotificationType.ApprovalRequested && n.ObsoleteAtUtc is null));

    public Task<bool> ExistsAsync(string dedupeKey, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Any(n => n.DedupeKey == dedupeKey));

    public Task<bool> IsWithdrawnAsync(Guid approvalRequestId, CancellationToken cancellationToken) =>
        Task.FromResult(Withdrawn.Contains(approvalRequestId));

    // The real one picks and pushes in one statement; here the rows come back as they are.
    public Task<IReadOnlyList<Notification>> ClaimDueEmailsAsync(DateTime nowUtc, TimeSpan lease, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(Items
            .Where(n => n.EmailStatus == NotificationEmailStatus.Pending && n.EmailNextAttemptAtUtc <= nowUtc)
            .OrderBy(n => n.EmailNextAttemptAtUtc)
            .Take(limit)
            .ToList());

    public Task<IReadOnlyList<Notification>> ListInboxAsync(Guid userId, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(Items
            .Where(n => n.RecipientUserId == userId && n.ObsoleteAtUtc is null)
            .OrderByDescending(n => n.CreatedAtUtc)
            .ThenByDescending(n => n.Id)
            .Take(limit)
            .ToList());

    public Task<int> CountUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.Count(n => n.RecipientUserId == userId && n.ObsoleteAtUtc is null && !n.IsRead));

    public Task<Notification?> GetInboxItemAsync(Guid userId, Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(n => n.Id == id && n.RecipientUserId == userId && n.ObsoleteAtUtc is null));

    public Task<IReadOnlyList<Notification>> ListUnreadAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Notification>>(Items.Where(n => n.RecipientUserId == userId && n.ObsoleteAtUtc is null && !n.IsRead).ToList());

    public void Add(Notification notification) => Items.Add(notification);

    public void AddWithdrawn(WithdrawnRequest withdrawn) => Withdrawn.Add(withdrawn.Id);
}
