namespace Partnr.Integrations.Application.Common;

internal enum AttemptOutcome
{
    Pushed,
    Failed,
    NeedsReconnect,
}

/// <summary>How one try to bring a meeting's Google event up to date ended.</summary>
internal sealed record MeetingSyncAttempt(AttemptOutcome Outcome, string? EventId = null, string? MeetUrl = null);
