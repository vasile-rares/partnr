namespace Partnr.Application.Common;

internal enum MeetingSyncOutcome
{
    Pushed,
    Failed,
    NeedsReconnect,
}

/// <summary>How one try to bring a meeting's Google event up to date ended.</summary>
internal sealed record MeetingSyncAttempt(MeetingSyncOutcome Outcome, string? EventId = null, string? MeetUrl = null);
