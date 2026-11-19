namespace Partnr.Contracts.Events;

/// <summary>Where a meeting's Google event ended up. Only final answers are sent: a try that will be repeated later says nothing.</summary>
public enum MeetingSyncOutcome
{
    Synced,

    /// <summary>Tried several times, or the meeting is over: it gave up. A member can ask for another go.</summary>
    Failed,

    /// <summary>The organiser's Google permission is gone: nothing is tried until they connect again.</summary>
    NeedsReconnect,
}

/// <param name="Version">The version of the meeting this answer is about; the receiver ignores it if the meeting has moved on.</param>
/// <param name="MeetUrl">The Meet link of the event, when there is one.</param>
public sealed record MeetingSynced(Guid MeetingId, int Version, MeetingSyncOutcome Outcome, string? MeetUrl);
