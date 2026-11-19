namespace Partnr.Notifications.Application.Interfaces.Services;

/// <summary>Tells connected browsers that an account's inbox changed. A signal with no data: the browser asks the REST routes what is new.</summary>
public interface IInboxPublisher
{
    /// <summary>Never throws: a push that is missed is repaired the next time the page loads or the connection comes back.</summary>
    Task InboxChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken);
}
