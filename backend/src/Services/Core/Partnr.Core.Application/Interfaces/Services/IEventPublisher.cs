namespace Partnr.Core.Application.Interfaces.Services;

/// <summary>
/// How a service tells the other services that something happened. Like the notification writer it is called before
/// <c>SaveChangesAsync</c>: the event leaves exactly when the change it describes is saved, and not at all if the save fails.
/// </summary>
public interface IEventPublisher
{
    /// <param name="event">A type from <c>Partnr.Contracts.Events</c>.</param>
    ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull;
}
