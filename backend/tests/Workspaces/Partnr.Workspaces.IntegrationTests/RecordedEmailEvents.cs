using System.Collections.Concurrent;
using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.IntegrationTests;

/// <summary>
/// What the API said should be emailed (the Notifications service writes the email). Shared by all tests (they run in parallel), so
/// tests look up events by recipient.
/// </summary>
public sealed class RecordedEmailEvents
{
    private readonly ConcurrentQueue<ClientInvited> _invitations = new();
    private readonly ConcurrentQueue<PasswordResetRequested> _resets = new();

    public IReadOnlyList<ClientInvited> InvitationsTo(string email) =>
        _invitations.Where(e => string.Equals(e.RecipientEmail, email, StringComparison.OrdinalIgnoreCase)).ToList();

    public IReadOnlyList<PasswordResetRequested> ResetsTo(string email) =>
        _resets.Where(e => string.Equals(e.RecipientEmail, email, StringComparison.OrdinalIgnoreCase)).ToList();

    internal void Record(object @event)
    {
        switch (@event)
        {
            case ClientInvited invited:
                _invitations.Enqueue(invited);
                break;
            case PasswordResetRequested reset:
                _resets.Enqueue(reset);
                break;
        }
    }
}

/// <summary>Passes every event on to the real publisher and keeps the ones about emails.</summary>
internal sealed class RecordingEventPublisher(IEventPublisher inner, RecordedEmailEvents recorded) : IEventPublisher
{
    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        recorded.Record(@event);

        return inner.PublishAsync(@event, cancellationToken);
    }
}
