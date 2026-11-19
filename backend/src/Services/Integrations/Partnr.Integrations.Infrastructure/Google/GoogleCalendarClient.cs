using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Partnr.Integrations.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Partnr.Integrations.Infrastructure.Google;

/// <summary>Google Calendar's REST API over plain HttpClient, like the OAuth client. Events live in the organiser's primary calendar.</summary>
internal sealed class GoogleCalendarClient(HttpClient http, IOptions<GoogleOptions> options, ILogger<GoogleCalendarClient> logger)
    : IGoogleCalendarClient
{
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string EventsEndpoint = "https://www.googleapis.com/calendar/v3/calendars/primary/events";

    // conferenceDataVersion lets the call make a Meet link; sendUpdates makes Google email the client the invitation and every change.
    private const string Query = "conferenceDataVersion=1&sendUpdates=all";

    private GoogleOptions Settings => options.Value;

    public async Task<GoogleEventResult> UpsertEventAsync(
        string refreshToken,
        string eventId,
        bool exists,
        GoogleEventDetails details,
        CancellationToken cancellationToken)
    {
        try
        {
            var access = await AccessTokenAsync(refreshToken, cancellationToken);

            if (!exists)
            {
                var created = await SendAsync(HttpMethod.Post, $"{EventsEndpoint}?{Query}", access, InsertBody(eventId, details), cancellationToken, HttpStatusCode.Conflict);
                if (created.Status != HttpStatusCode.Conflict)
                {
                    return new GoogleEventResult(eventId, MeetUrlIn(created.Body));
                }

                // 409: an earlier try reached Google but its answer never reached us. The event is there, so it is brought up to date.
            }

            var updated = await SendAsync(
                new HttpMethod("PATCH"),
                $"{EventsEndpoint}/{Uri.EscapeDataString(eventId)}?{Query}",
                access,
                PatchBody(details),
                cancellationToken,
                HttpStatusCode.NotFound,
                HttpStatusCode.Gone);

            // The organiser deleted it in Google: it is not brought back.
            return new GoogleEventResult(eventId, updated.Body is null ? null : MeetUrlIn(updated.Body));
        }
        catch (Exception ex) when (IsTransient(ex, cancellationToken))
        {
            throw Wrap(ex);
        }
    }

    public async Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken cancellationToken)
    {
        try
        {
            var access = await AccessTokenAsync(refreshToken, cancellationToken);
            await SendAsync(
                HttpMethod.Delete,
                $"{EventsEndpoint}/{Uri.EscapeDataString(eventId)}?sendUpdates=all",
                access,
                null,
                cancellationToken,
                HttpStatusCode.NotFound,
                HttpStatusCode.Gone);
        }
        catch (Exception ex) when (IsTransient(ex, cancellationToken))
        {
            throw Wrap(ex);
        }
    }

    private async Task<string> AccessTokenAsync(string refreshToken, CancellationToken cancellationToken)
    {
        using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["client_id"] = Settings.ClientId,
            ["client_secret"] = Settings.ClientSecret,
            ["refresh_token"] = refreshToken,
            ["grant_type"] = "refresh_token",
        }), cancellationToken);

        var body = Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        if (!response.IsSuccessStatusCode)
        {
            // invalid_grant is Google saying the permission was revoked or has lapsed; anything else is worth another try.
            logger.LogWarning("Google would not refresh the access token: {Status} {Error}", (int)response.StatusCode, Text(body, "error"));
            if (response.StatusCode == HttpStatusCode.BadRequest && Text(body, "error") == "invalid_grant")
            {
                throw new GoogleAccessRevokedException("Google no longer accepts the saved permission.");
            }

            throw new GoogleCalendarException($"Google would not give an access token: {(int)response.StatusCode} {Text(body, "error")}");
        }

        return Text(body, "access_token") ?? throw new GoogleCalendarException("Google's answer had no access token.");
    }

    // `accepted` are statuses the caller handles itself; every other failure is an exception.
    private async Task<(HttpStatusCode Status, JsonNode? Body)> SendAsync(
        HttpMethod method,
        string url,
        string accessToken,
        JsonNode? body,
        CancellationToken cancellationToken,
        params HttpStatusCode[] accepted)
    {
        using var request = new HttpRequestMessage(method, url);
        request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        if (body is not null)
        {
            request.Content = new StringContent(body.ToJsonString(), Encoding.UTF8, "application/json");
        }

        using var response = await http.SendAsync(request, cancellationToken);
        var text = await response.Content.ReadAsStringAsync(cancellationToken);
        var json = Parse(text);

        if (response.IsSuccessStatusCode || accepted.Contains(response.StatusCode))
        {
            return (response.StatusCode, json);
        }

        var reason = json?["error"]?["errors"]?[0]?["reason"]?.GetValue<string>();
        // The reason is what tells "the Calendar API is not enabled" (accessNotConfigured) from a busy Google, so it is kept in the log.
        logger.LogWarning("Google Calendar answered {Status} {Reason} for {Method} {Path}", (int)response.StatusCode, reason, method.Method, new Uri(url).AbsolutePath);
        if (response.StatusCode == HttpStatusCode.Unauthorized || (response.StatusCode == HttpStatusCode.Forbidden && reason == "insufficientPermissions"))
        {
            throw new GoogleAccessRevokedException("Google no longer lets us use the calendar.");
        }

        throw new GoogleCalendarException($"Google answered {(int)response.StatusCode} {reason}");
    }

    private static JsonObject InsertBody(string eventId, GoogleEventDetails details)
    {
        var body = PatchBody(details);
        body["id"] = eventId;
        body["attendees"] = new JsonArray(new JsonObject { ["email"] = details.AttendeeEmail });
        body["conferenceData"] = new JsonObject
        {
            ["createRequest"] = new JsonObject
            {
                ["requestId"] = eventId,
                ["conferenceSolutionKey"] = new JsonObject { ["type"] = "hangoutsMeet" },
            },
        };
        // The calendar's own reminders, whatever the organiser has set up there.
        body["reminders"] = new JsonObject { ["useDefault"] = true };

        return body;
    }

    private static JsonObject PatchBody(GoogleEventDetails details) => new()
    {
        ["summary"] = details.Title,
        // An empty text clears the description in Google; leaving it out would keep the old one.
        ["description"] = details.Description ?? string.Empty,
        ["start"] = Moment(details.StartsAtUtc),
        ["end"] = Moment(details.EndsAtUtc),
    };

    private static JsonObject Moment(DateTime utc) => new()
    {
        ["dateTime"] = utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture),
        ["timeZone"] = "UTC",
    };

    private static string? MeetUrlIn(JsonNode? eventJson)
    {
        if (eventJson?["hangoutLink"]?.GetValue<string>() is { Length: > 0 } link)
        {
            return link;
        }

        return eventJson?["conferenceData"]?["entryPoints"]?.AsArray()
            .FirstOrDefault(p => p?["entryPointType"]?.GetValue<string>() == "video")?["uri"]?.GetValue<string>();
    }

    private static JsonNode? Parse(string text)
    {
        try
        {
            return string.IsNullOrWhiteSpace(text) ? null : JsonNode.Parse(text);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static string? Text(JsonNode? node, string name) => node?[name]?.GetValue<string>();

    // A refused permission is its own exception and keeps its meaning; a cancellation by the caller is not an error at all.
    private static bool IsTransient(Exception ex, CancellationToken cancellationToken) =>
        ex is not (GoogleAccessRevokedException or GoogleCalendarException)
        && ex is HttpRequestException or TaskCanceledException or JsonException or InvalidOperationException or FormatException
        && !cancellationToken.IsCancellationRequested;

    private GoogleCalendarException Wrap(Exception ex)
    {
        logger.LogWarning(ex, "Google Calendar could not be reached");
        return new GoogleCalendarException("Google Calendar could not be reached.", ex);
    }
}
