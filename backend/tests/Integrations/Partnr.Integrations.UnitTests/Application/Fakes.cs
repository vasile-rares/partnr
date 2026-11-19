using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.UnitTests.Application;

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

internal sealed class FakeGoogleConnectionRepository : IGoogleConnectionRepository
{
    public List<GoogleConnection> Items { get; } = [];

    public Task<GoogleConnection?> GetForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(c => c.UserId == userId));

    public void Add(GoogleConnection connection) => Items.Add(connection);

    public void Remove(GoogleConnection connection) => Items.Remove(connection);
}

internal sealed class FakeGoogleOAuthClient : IGoogleOAuthClient
{
    public bool IsConfigured { get; set; } = true;

    /// <summary>What Google answers to a code; null is a refusal.</summary>
    public GoogleGrant? Grant { get; set; } = new("refresh-1", "ana@gmail.example", true);

    public List<string> Revoked { get; } = [];

    public string AuthorizationUrl(string state) => $"https://google.test/consent?state={state}";

    public Task<GoogleGrant?> ExchangeCodeAsync(string code, CancellationToken cancellationToken) => Task.FromResult(Grant);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        Revoked.Add(refreshToken);
        return Task.CompletedTask;
    }
}

internal sealed class FakeSecretProtector : ISecretProtector
{
    public string Protect(string purpose, string plaintext) => $"{purpose}:{plaintext}".Replace('|', '~');

    public string? Unprotect(string purpose, string protectedValue) =>
        protectedValue.StartsWith($"{purpose}:", StringComparison.Ordinal)
            ? protectedValue[(purpose.Length + 1)..].Replace('~', '|')
            : null;
}

internal sealed class FakeGoogleCalendarClient : IGoogleCalendarClient
{
    public sealed record Call(string Operation, string Token, string EventId, bool Exists, GoogleEventDetails? Details);

    public List<Call> Calls { get; } = [];

    /// <summary>Thrown by the next calls (instead of answering) until it is cleared.</summary>
    public Exception? Throw { get; set; }

    /// <summary>Runs while "Google" is answering: a member changing the meeting at that very moment.</summary>
    public Action? DuringCall { get; set; }

    public Task<GoogleEventResult> UpsertEventAsync(string refreshToken, string eventId, bool exists, GoogleEventDetails details, CancellationToken cancellationToken)
    {
        Calls.Add(new Call("upsert", refreshToken, eventId, exists, details));
        Answer();

        return Task.FromResult(new GoogleEventResult(eventId, $"https://meet.google.com/{eventId[..3]}-meet"));
    }

    public Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken cancellationToken)
    {
        Calls.Add(new Call("delete", refreshToken, eventId, true, null));
        Answer();

        return Task.CompletedTask;
    }

    private void Answer()
    {
        DuringCall?.Invoke();
        if (Throw is not null)
        {
            throw Throw;
        }
    }
}

internal sealed class FakeMeetingSyncRepository : IMeetingSyncRepository
{
    public List<MeetingSync> Items { get; } = [];

    public Task<MeetingSync?> GetByIdAsync(Guid meetingId, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(m => m.Id == meetingId));

    // The real one picks and pushes in one statement; here the rows come back as copies would, so the caller must load them again.
    public Task<IReadOnlyList<MeetingSync>> ClaimDueAsync(DateTime nowUtc, TimeSpan lease, int limit, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MeetingSync>>(Items
            .Where(m => m.Status == MeetingSyncStatus.Pending && m.NextSyncAtUtc <= nowUtc)
            .OrderBy(m => m.NextSyncAtUtc)
            .Take(limit)
            .ToList());

    public Task<IReadOnlyList<MeetingSync>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<MeetingSync>>(Items
            .Where(m => m.OrganizerUserId == organizerUserId && m.EndsAtUtc > nowUtc && m.Status != MeetingSyncStatus.Synced)
            .ToList());

    public void Add(MeetingSync meetingSync) => Items.Add(meetingSync);
}

internal sealed class FakeEventPublisher : IEventPublisher
{
    public List<object> Published { get; } = [];

    public ValueTask PublishAsync<TEvent>(TEvent @event, CancellationToken cancellationToken = default)
        where TEvent : notnull
    {
        Published.Add(@event);
        return ValueTask.CompletedTask;
    }
}
