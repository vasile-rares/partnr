namespace Partnr.Contracts.Events;

/// <summary>A member asked for another go at sending a meeting to Google after the tries ran out.</summary>
public sealed record MeetingSyncRetryRequested(Guid MeetingId);
