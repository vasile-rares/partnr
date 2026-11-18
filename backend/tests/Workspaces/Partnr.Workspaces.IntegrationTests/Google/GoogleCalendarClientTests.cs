using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Infrastructure.Google;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Partnr.Workspaces.IntegrationTests.Google;

// No container and no Google: the HTTP handler is replaced by a fake that records what was sent.
public class GoogleCalendarClientTests
{
    private const string EventId = "0190a1b2c3d4e5f60718293a4b5c6d7e";

    private static readonly GoogleEventDetails Details =
        new("Kick-off", "Bring the brief", new DateTime(2026, 11, 20, 9, 30, 0, DateTimeKind.Utc), new DateTime(2026, 11, 20, 10, 0, 0, DateTimeKind.Utc), "elena@example.com");

    private readonly FakeGoogle _google = new();
    private readonly GoogleCalendarClient _client;

    public GoogleCalendarClientTests()
    {
        var options = Options.Create(new GoogleOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "http://localhost:3000/api/integrations/google/callback",
            EncryptionKey = GoogleOptions.NewKey(),
        });
        _client = new GoogleCalendarClient(new HttpClient(_google), options, NullLogger<GoogleCalendarClient>.Instance);
    }

    [Fact]
    public async Task Create_AsksForAMeetLinkAndAnInvitation_WithTheClientAsTheGuest()
    {
        _google.OnEvents = _ => (HttpStatusCode.OK, "{\"id\":\"" + EventId + "\",\"hangoutLink\":\"https://meet.google.com/abc-defg-hij\"}");

        var result = await _client.UpsertEventAsync("refresh-1", EventId, exists: false, Details, CancellationToken.None);

        Assert.Equal(new GoogleEventResult(EventId, "https://meet.google.com/abc-defg-hij"), result);

        var token = _google.Sent[0];
        Assert.Equal("https://oauth2.googleapis.com/token", token.Url);
        Assert.Contains("refresh_token=refresh-1", token.Body);
        Assert.Contains("grant_type=refresh_token", token.Body);

        var insert = _google.Sent[1];
        Assert.Equal((HttpMethod.Post.Method, "Bearer access-1"), (insert.Method, insert.Authorization));
        Assert.Equal("https://www.googleapis.com/calendar/v3/calendars/primary/events?conferenceDataVersion=1&sendUpdates=all", insert.Url);
        var body = JsonNode.Parse(insert.Body)!;
        Assert.Equal(EventId, (string?)body["id"]);
        Assert.Equal("Kick-off", (string?)body["summary"]);
        Assert.Equal("Bring the brief", (string?)body["description"]);
        Assert.Equal("2026-11-20T09:30:00Z", (string?)body["start"]!["dateTime"]);
        Assert.Equal("2026-11-20T10:00:00Z", (string?)body["end"]!["dateTime"]);
        Assert.Equal("elena@example.com", (string?)body["attendees"]![0]!["email"]);
        Assert.Equal("hangoutsMeet", (string?)body["conferenceData"]!["createRequest"]!["conferenceSolutionKey"]!["type"]);
        Assert.Equal(EventId, (string?)body["conferenceData"]!["createRequest"]!["requestId"]);
        Assert.True((bool?)body["reminders"]!["useDefault"]);
    }

    [Fact]
    public async Task Create_TakesTheMeetLinkFromTheEntryPoints_WhenThereIsNoShortLink()
    {
        _google.OnEvents = _ => (HttpStatusCode.OK, """
            {"conferenceData":{"entryPoints":[{"entryPointType":"phone","uri":"tel:+1"},{"entryPointType":"video","uri":"https://meet.google.com/xyz-abcd-efg"}]}}
            """);

        var result = await _client.UpsertEventAsync("refresh-1", EventId, false, Details, CancellationToken.None);

        Assert.Equal("https://meet.google.com/xyz-abcd-efg", result.MeetUrl);
    }

    [Fact]
    public async Task Create_WhenTheEventIsThereAlready_UpdatesItInstead()
    {
        // An earlier try reached Google but its answer was lost: the id is ours, so the second try finds the event.
        var calls = 0;
        _google.OnEvents = request => ++calls == 1
            ? (HttpStatusCode.Conflict, "{\"error\":{\"errors\":[{\"reason\":\"duplicate\"}]}}")
            : (HttpStatusCode.OK, "{\"hangoutLink\":\"https://meet.google.com/abc-defg-hij\"}");

        var result = await _client.UpsertEventAsync("refresh-1", EventId, exists: false, Details, CancellationToken.None);

        Assert.Equal("https://meet.google.com/abc-defg-hij", result.MeetUrl);
        Assert.Equal(["POST", "PATCH"], _google.Sent.Skip(1).Select(s => s.Method));
        Assert.EndsWith($"/events/{EventId}?conferenceDataVersion=1&sendUpdates=all", _google.Sent[2].Url);
    }

    [Fact]
    public async Task Update_SendsOnlyWhatChanges_AndAnEmptyNoteClearsTheDescription()
    {
        _google.OnEvents = _ => (HttpStatusCode.OK, "{\"hangoutLink\":\"https://meet.google.com/abc-defg-hij\"}");

        await _client.UpsertEventAsync("refresh-1", EventId, exists: true, Details with { Description = null }, CancellationToken.None);

        var patch = _google.Sent[1];
        Assert.Equal("PATCH", patch.Method);
        var body = JsonNode.Parse(patch.Body)!.AsObject();
        Assert.Equal(["summary", "description", "start", "end"], body.Select(p => p.Key));
        Assert.Equal(string.Empty, (string?)body["description"]);
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task Update_OfAnEventTheOrganiserDeleted_IsNotBroughtBack_AndIsNotAnError(HttpStatusCode status)
    {
        _google.OnEvents = _ => (status, "{}");

        var result = await _client.UpsertEventAsync("refresh-1", EventId, exists: true, Details, CancellationToken.None);

        Assert.Equal(new GoogleEventResult(EventId, null), result);
        Assert.Equal(2, _google.Sent.Count); // the token and the patch: no new insert
    }

    [Fact]
    public async Task Delete_TellsGoogleToNotifyTheClient()
    {
        _google.OnEvents = _ => (HttpStatusCode.NoContent, string.Empty);

        await _client.DeleteEventAsync("refresh-1", EventId, CancellationToken.None);

        var delete = _google.Sent[1];
        Assert.Equal(("DELETE", $"https://www.googleapis.com/calendar/v3/calendars/primary/events/{EventId}?sendUpdates=all"), (delete.Method, delete.Url));
    }

    [Theory]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.Gone)]
    public async Task Delete_OfAnEventThatIsGone_CountsAsDeleted(HttpStatusCode status)
    {
        _google.OnEvents = _ => (status, "{}");

        await _client.DeleteEventAsync("refresh-1", EventId, CancellationToken.None);
    }

    [Fact]
    public async Task ARefreshTokenGoogleRejects_MeansTheAccessWasRevoked()
    {
        _google.OnToken = () => (HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\",\"error_description\":\"Token has been expired or revoked.\"}");

        await Assert.ThrowsAsync<GoogleAccessRevokedException>(() => _client.UpsertEventAsync("refresh-1", EventId, false, Details, CancellationToken.None));
        await Assert.ThrowsAsync<GoogleAccessRevokedException>(() => _client.DeleteEventAsync("refresh-1", EventId, CancellationToken.None));
        Assert.DoesNotContain(_google.Sent, s => s.Url.Contains("/calendar/"));
    }

    [Theory]
    [InlineData(HttpStatusCode.Unauthorized, "{}")]
    [InlineData(HttpStatusCode.Forbidden, "{\"error\":{\"errors\":[{\"reason\":\"insufficientPermissions\"}]}}")]
    public async Task ACalendarCallGoogleRefusesForPermission_MeansTheAccessWasRevoked(HttpStatusCode status, string body)
    {
        _google.OnEvents = _ => (status, body);

        await Assert.ThrowsAsync<GoogleAccessRevokedException>(() => _client.UpsertEventAsync("refresh-1", EventId, false, Details, CancellationToken.None));
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError, "{}")]
    [InlineData(HttpStatusCode.ServiceUnavailable, "")]
    [InlineData(HttpStatusCode.TooManyRequests, "{\"error\":{\"errors\":[{\"reason\":\"rateLimitExceeded\"}]}}")]
    [InlineData(HttpStatusCode.Forbidden, "{\"error\":{\"errors\":[{\"reason\":\"userRateLimitExceeded\"}]}}")]
    [InlineData(HttpStatusCode.BadRequest, "not json")]
    public async Task OtherFailuresAreWorthAnotherTry(HttpStatusCode status, string body)
    {
        _google.OnEvents = _ => (status, body);

        await Assert.ThrowsAsync<GoogleCalendarException>(() => _client.UpsertEventAsync("refresh-1", EventId, false, Details, CancellationToken.None));
        await Assert.ThrowsAsync<GoogleCalendarException>(() => _client.DeleteEventAsync("refresh-1", EventId, CancellationToken.None));
    }

    [Fact]
    public async Task ATokenCallThatFailsForAnotherReason_IsWorthAnotherTry()
    {
        _google.OnToken = () => (HttpStatusCode.InternalServerError, "{}");

        await Assert.ThrowsAsync<GoogleCalendarException>(() => _client.UpsertEventAsync("refresh-1", EventId, false, Details, CancellationToken.None));
    }

    [Fact]
    public async Task WhenGoogleCannotBeReached_ItIsWorthAnotherTry()
    {
        _google.Fail = new HttpRequestException("no network");

        await Assert.ThrowsAsync<GoogleCalendarException>(() => _client.UpsertEventAsync("refresh-1", EventId, false, Details, CancellationToken.None));
        await Assert.ThrowsAsync<GoogleCalendarException>(() => _client.DeleteEventAsync("refresh-1", EventId, CancellationToken.None));
    }

    [Fact]
    public async Task ACancellationByTheCaller_IsNotHiddenAsAFailure()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        _google.Fail = new TaskCanceledException();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(() => _client.UpsertEventAsync("refresh-1", EventId, false, Details, cancelled.Token));
    }

    private sealed class FakeGoogle : HttpMessageHandler
    {
        public record Request(string Method, string Url, string Body, string? Authorization);

        public List<Request> Sent { get; } = [];

        public Exception? Fail { get; set; }

        public Func<(HttpStatusCode, string)> OnToken { get; set; } = () => (HttpStatusCode.OK, "{\"access_token\":\"access-1\"}");

        public Func<HttpRequestMessage, (HttpStatusCode, string)> OnEvents { get; set; } = _ => (HttpStatusCode.OK, "{}");

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var body = request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken);
            Sent.Add(new Request(request.Method.Method, request.RequestUri!.ToString(), body, request.Headers.Authorization?.ToString()));

            if (Fail is not null)
            {
                throw Fail;
            }

            var (status, text) = request.RequestUri!.Host == "oauth2.googleapis.com" ? OnToken() : OnEvents(request);

            return new HttpResponseMessage(status) { Content = new StringContent(text, Encoding.UTF8, "application/json") };
        }
    }
}
