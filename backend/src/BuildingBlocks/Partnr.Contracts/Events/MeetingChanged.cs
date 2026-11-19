namespace Partnr.Contracts.Events;

/// <summary>
/// A member scheduled, changed or cancelled a meeting. It carries everything the calendar needs, so whoever consumes it never calls
/// back. <see cref="Version"/> grows with every change of the meeting: a consumer ignores an event that is not newer than what it has.
/// </summary>
public sealed record MeetingChanged(
    Guid MeetingId,
    int Version,
    Guid OrganizerUserId,
    string Title,
    string? Description,
    DateTime StartsAtUtc,
    DateTime EndsAtUtc,
    string ClientEmail,
    bool Cancelled);
