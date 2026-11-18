using System.Buffers.Text;
using System.Net;
using System.Text;
using Partnr.Workspaces.Infrastructure.Google;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;

namespace Partnr.Workspaces.IntegrationTests.Google;

// No container and no Google: the HTTP handler is replaced by a fake that records what was sent.
public class GoogleOAuthClientTests
{
    private const string Calendar = "https://www.googleapis.com/auth/calendar.events";

    private readonly FakeGoogle _google = new();
    private readonly GoogleOAuthClient _client;

    public GoogleOAuthClientTests()
    {
        var options = Options.Create(new GoogleOptions
        {
            ClientId = "client-id",
            ClientSecret = "client-secret",
            RedirectUri = "http://localhost:3000/api/integrations/google/callback",
            EncryptionKey = GoogleOptions.NewKey(),
        });
        _client = new GoogleOAuthClient(new HttpClient(_google), options, NullLogger<GoogleOAuthClient>.Instance);
    }

    [Fact]
    public void IsConfigured_NeedsAnIdAndASecret()
    {
        Assert.True(_client.IsConfigured);

        var off = new GoogleOAuthClient(new HttpClient(_google), Options.Create(new GoogleOptions()), NullLogger<GoogleOAuthClient>.Instance);
        Assert.False(off.IsConfigured);
    }

    [Fact]
    public void AuthorizationUrl_AsksForOfflineAccessToTheCalendarAndCarriesTheState()
    {
        var url = new Uri(_client.AuthorizationUrl("the state&more"));
        var query = System.Web.HttpUtility.ParseQueryString(url.Query);

        Assert.Equal("accounts.google.com", url.Host);
        Assert.Equal("client-id", query["client_id"]);
        Assert.Equal("http://localhost:3000/api/integrations/google/callback", query["redirect_uri"]);
        Assert.Equal("code", query["response_type"]);
        Assert.Equal("offline", query["access_type"]);
        Assert.Equal("consent", query["prompt"]);
        Assert.Equal("the state&more", query["state"]);
        Assert.Equal($"openid email {Calendar}", query["scope"]);
    }

    [Fact]
    public async Task ExchangeCode_ReadsTheRefreshTokenTheScopesAndTheEmail()
    {
        _google.Reply(HttpStatusCode.OK, Json(refresh: "1//token", idToken: IdToken("ana@gmail.example"), scope: $"openid email {Calendar}"));

        var grant = await _client.ExchangeCodeAsync("the-code", CancellationToken.None);

        Assert.Equal(new Partnr.Workspaces.Application.Interfaces.Services.GoogleGrant("1//token", "ana@gmail.example", true), grant);
        var sent = Assert.Single(_google.Sent);
        Assert.Equal("https://oauth2.googleapis.com/token", sent.Url);
        Assert.Contains("code=the-code", sent.Body);
        Assert.Contains("client_secret=client-secret", sent.Body);
        Assert.Contains("grant_type=authorization_code", sent.Body);
    }

    [Fact]
    public async Task ExchangeCode_WhenTheCalendarWasNotGranted_SaysSo()
    {
        _google.Reply(HttpStatusCode.OK, Json(refresh: "1//token", idToken: IdToken("ana@gmail.example"), scope: "openid email"));

        var grant = await _client.ExchangeCodeAsync("code", CancellationToken.None);

        Assert.False(grant!.CanUseCalendar);
    }

    [Fact]
    public async Task ExchangeCode_WithNoRefreshTokenOrIdToken_ReturnsThemEmpty()
    {
        _google.Reply(HttpStatusCode.OK, Json(refresh: null, idToken: null, scope: Calendar));

        var grant = await _client.ExchangeCodeAsync("code", CancellationToken.None);

        Assert.Null(grant!.RefreshToken);
        Assert.Null(grant.Email);
    }

    [Fact]
    public async Task ExchangeCode_WhenGoogleRefuses_ReturnsNull()
    {
        _google.Reply(HttpStatusCode.BadRequest, "{\"error\":\"invalid_grant\"}");

        Assert.Null(await _client.ExchangeCodeAsync("code", CancellationToken.None));
    }

    [Theory]
    [InlineData("not json")]
    [InlineData("")]
    public async Task ExchangeCode_OfAnAnswerThatIsNotJson_ReturnsNull(string body)
    {
        _google.Reply(HttpStatusCode.OK, body);

        Assert.Null(await _client.ExchangeCodeAsync("code", CancellationToken.None));
    }

    [Fact]
    public async Task ExchangeCode_WhenGoogleCannotBeReached_ReturnsNull()
    {
        _google.Fail = new HttpRequestException("no network");

        Assert.Null(await _client.ExchangeCodeAsync("code", CancellationToken.None));
    }

    [Fact]
    public async Task Revoke_SendsTheToken_AndNeverThrows()
    {
        _google.Reply(HttpStatusCode.OK, "{}");
        await _client.RevokeAsync("1//token", CancellationToken.None);
        Assert.Equal("https://oauth2.googleapis.com/revoke", _google.Sent[0].Url);
        Assert.Contains("token=1%2F%2Ftoken", _google.Sent[0].Body);

        _google.Reply(HttpStatusCode.BadRequest, "{}");
        await _client.RevokeAsync("1//token", CancellationToken.None);

        _google.Fail = new HttpRequestException("no network");
        await _client.RevokeAsync("1//token", CancellationToken.None);
    }

    private static string Json(string? refresh, string? idToken, string scope)
    {
        var parts = new List<string> { "\"access_token\":\"ya29.x\"", $"\"scope\":\"{scope}\"" };
        if (refresh is not null)
        {
            parts.Add($"\"refresh_token\":\"{refresh}\"");
        }

        if (idToken is not null)
        {
            parts.Add($"\"id_token\":\"{idToken}\"");
        }

        return "{" + string.Join(',', parts) + "}";
    }

    private static string IdToken(string email)
    {
        var payload = Base64Url.EncodeToString(Encoding.UTF8.GetBytes($"{{\"email\":\"{email}\",\"sub\":\"1\"}}"));

        return $"header.{payload}.signature";
    }

    private sealed class FakeGoogle : HttpMessageHandler
    {
        private HttpStatusCode _status = HttpStatusCode.OK;
        private string _body = "{}";

        public List<(string Url, string Body)> Sent { get; } = [];

        public Exception? Fail { get; set; }

        public void Reply(HttpStatusCode status, string body)
        {
            Fail = null;
            _status = status;
            _body = body;
        }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Sent.Add((request.RequestUri!.ToString(), request.Content is null ? string.Empty : await request.Content.ReadAsStringAsync(cancellationToken)));

            return Fail is null
                ? new HttpResponseMessage(_status) { Content = new StringContent(_body, Encoding.UTF8, "application/json") }
                : throw Fail;
        }
    }
}
