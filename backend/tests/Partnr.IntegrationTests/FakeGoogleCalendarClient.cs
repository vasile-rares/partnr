using System.Collections.Concurrent;
using Partnr.Application.Interfaces.Services;

namespace Partnr.IntegrationTests;

/// <summary>Stands in for Google Calendar. What goes wrong is set per refresh token, so a test only affects its own organiser.</summary>
public sealed class FakeGoogleCalendarClient : IGoogleCalendarClient
{
    public sealed record Call(string Token, string Operation, string EventId, bool Exists, GoogleEventDetails? Details);

    private readonly ConcurrentQueue<Call> _calls = new();

    public ConcurrentDictionary<string, Exception> Failures { get; } = new();

    public ConcurrentDictionary<string, TimeSpan> Delays { get; } = new();

    public IReadOnlyList<Call> CallsFor(string token) => _calls.Where(c => c.Token == token).ToList();

    public async Task<GoogleEventResult> UpsertEventAsync(string refreshToken, string eventId, bool exists, GoogleEventDetails details, CancellationToken cancellationToken)
    {
        await Answer(new Call(refreshToken, "upsert", eventId, exists, details), cancellationToken);

        return new GoogleEventResult(eventId, $"https://meet.google.com/{eventId[..3]}-{eventId[3..7]}-{eventId[7..10]}");
    }

    public Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken cancellationToken) =>
        Answer(new Call(refreshToken, "delete", eventId, true, null), cancellationToken);

    private async Task Answer(Call call, CancellationToken cancellationToken)
    {
        _calls.Enqueue(call);
        if (Delays.TryGetValue(call.Token, out var delay))
        {
            await Task.Delay(delay, cancellationToken);
        }

        if (Failures.TryGetValue(call.Token, out var failure))
        {
            throw failure;
        }
    }
}
