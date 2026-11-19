namespace Partnr.Integrations.Application.Interfaces.Services;

public sealed record GoogleEventDetails(string Title, string? Description, DateTime StartsAtUtc, DateTime EndsAtUtc, string AttendeeEmail);

/// <summary><see cref="MeetUrl"/> is null when Google did not say (an update of an event that is gone, for one).</summary>
public sealed record GoogleEventResult(string EventId, string? MeetUrl);

/// <summary>The organiser's Google Calendar, one event per meeting. Google sends the client the invitation and every later change.</summary>
public interface IGoogleCalendarClient
{
    /// <summary>
    /// Creates the event with a Meet link and the client invited, or brings an existing one to these details. An event the
    /// organiser deleted in Google is not brought back.
    /// </summary>
    Task<GoogleEventResult> UpsertEventAsync(string refreshToken, string eventId, bool exists, GoogleEventDetails details, CancellationToken cancellationToken);

    /// <summary>An event that is already gone counts as deleted.</summary>
    Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken cancellationToken);
}

/// <summary>Google no longer accepts the saved permission: retrying cannot help until the person connects again.</summary>
public sealed class GoogleAccessRevokedException(string message) : Exception(message);

/// <summary>Anything else that went wrong with Google (it is down, slow, busy): worth another try later.</summary>
public sealed class GoogleCalendarException(string message, Exception? inner = null) : Exception(message, inner);
