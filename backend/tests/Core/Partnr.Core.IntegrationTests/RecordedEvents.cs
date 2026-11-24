using System.Collections.Concurrent;
using Partnr.Contracts.Events;
using Partnr.Core.Application.Interfaces.Services;

namespace Partnr.Core.IntegrationTests;

/// <summary>
/// What the API said happened that other services act on (an email to send, bytes to remove). Shared by all tests (they run in
/// parallel), so tests look events up by recipient or key.
/// </summary>
public sealed class RecordedEvents
{
    private readonly ConcurrentQueue<ClientInvited> _invitations = new();
    private readonly ConcurrentQueue<PasswordResetRequested> _resets = new();
    private readonly ConcurrentQueue<ProjectFileRemoved> _filesRemoved = new();

    public IReadOnlyList<ProjectFileRemoved> FilesRemovedUnder(string keyPrefix) =>
        _filesRemoved.Where(e => e.StorageKey.StartsWith(keyPrefix, StringComparison.Ordinal)).ToList();

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
            case ProjectFileRemoved removed:
                _filesRemoved.Enqueue(removed);
                break;
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
