using System.Collections.Concurrent;
using Partnr.Contracts.Events;
using Partnr.Identity.Application.Interfaces.Services;

namespace Partnr.Identity.IntegrationTests;

/// <summary>
/// What the service said happened that other services act on (an email to send). Shared by all tests (they run in parallel),
/// so tests look events up by recipient.
/// </summary>
public sealed class RecordedEvents
{
    private readonly ConcurrentQueue<PasswordResetRequested> _resets = new();

    public IReadOnlyList<PasswordResetRequested> ResetsTo(string email) =>
        _resets.Where(e => string.Equals(e.RecipientEmail, email, StringComparison.OrdinalIgnoreCase)).ToList();

    internal void Record(object @event)
    {
        if (@event is PasswordResetRequested reset)
        {
            _resets.Enqueue(reset);
        }
    }
}

/// <summary>Passes every event on to the real publisher and keeps the ones the tests look at.</summary>
internal sealed class RecordingEventPublisher(IEventPublisher inner, RecordedEvents recorded) : IEventPublisher
{
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        recorded.Record(@event);

        return inner.PublishAsync(@event, cancellationToken);
    }
}
