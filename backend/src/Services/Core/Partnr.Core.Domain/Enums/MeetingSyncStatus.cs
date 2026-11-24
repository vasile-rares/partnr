namespace Partnr.Core.Domain.Enums;

/// <summary>Where the meeting's Google event stands. Every change to the meeting makes it <see cref="Pending"/> again.</summary>
public enum MeetingSyncStatus
{
    Pending,
    Synced,

    /// <summary>Tried several times (or the meeting is over) and gave up; a member can ask for another go.</summary>
    Failed,

    /// <summary>The organiser's Google permission is gone: nothing is tried until they connect again.</summary>
    NeedsReconnect,
}
