using System.Net;
using System.Net.Http.Json;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Partnr.Contracts;
using Partnr.Gateway.Sessions;

namespace Partnr.Gateway.Tests;

public sealed class SessionTests : IAsyncLifetime
{
    private const string SessionCookie = ".AspNetCore.Identity.Application=ticket";
    private const string AccountId = "7d8e1b5a-0000-0000-0000-000000000001";

    private readonly FakeSessions _sessions = new();
    private FakeApi _api = null!;

    public async Task InitializeAsync() => _api = await FakeApi.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task A_valid_session_reaches_the_api_as_a_short_jwt_naming_the_account()
    {
        _sessions.Next = new SessionCheck(SessionOutcome.Valid, AccountId);

        var received = await Get(SessionCookie);

        Assert.StartsWith("Bearer ", received.Authorization);
        var result = await new JsonWebTokenHandler().ValidateTokenAsync(received.Authorization["Bearer ".Length..], new TokenValidationParameters
        {
            ValidIssuer = InternalAuth.Issuer,
            ValidAudience = InternalAuth.Audience,
            IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(GatewayHost.SigningKey)),
            ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        });
        Assert.True(result.IsValid, result.Exception?.Message);
        var token = (JsonWebToken)result.SecurityToken;
        Assert.Equal(AccountId, token.Subject);
        Assert.Equal(InternalAuth.TokenLifetime, token.ValidTo - token.ValidFrom);
    }

    [Fact]
    public async Task The_whole_cookie_header_is_what_the_session_is_asked_about_and_it_still_reaches_the_api()
    {
        _sessions.Next = new SessionCheck(SessionOutcome.Valid, AccountId);
        var cookie = $"theme=dark; {SessionCookie}; other=1";

        var received = await Get(cookie);

        Assert.Equal(cookie, _sessions.LastCookie);
        Assert.Equal(cookie, received.Cookie);
    }

    [Fact]
    public async Task A_cookie_that_comes_in_pieces_is_still_a_session()
    {
        _sessions.Next = new SessionCheck(SessionOutcome.Valid, AccountId);

        var received = await Get(".AspNetCore.Identity.ApplicationC1=part");

        Assert.StartsWith("Bearer ", received.Authorization);
    }

    [Fact]
    public async Task An_invalid_session_goes_on_without_a_jwt()
    {
        _sessions.Next = new SessionCheck(SessionOutcome.Invalid);

        var received = await Get(SessionCookie);

        Assert.Equal("", received.Authorization);
    }

    [Fact]
    public async Task Without_a_session_cookie_nobody_is_asked_and_there_is_no_jwt()
    {
        var received = await Get("theme=dark");

        Assert.Equal(0, _sessions.Calls);
        Assert.Equal("", received.Authorization);
    }

    [Fact]
    public async Task An_authorization_header_from_the_caller_never_reaches_the_api()
    {
        _sessions.Next = new SessionCheck(SessionOutcome.Invalid);

        var withoutSession = await Get(cookie: null, authorization: "Bearer made-up");
        var withInvalidSession = await Get(SessionCookie, authorization: "Bearer made-up");

        Assert.Equal("", withoutSession.Authorization);
        Assert.Equal("", withInvalidSession.Authorization);
    }

    [Fact]
    public async Task When_the_sessions_cannot_be_checked_the_answer_is_service_unavailable_and_the_api_is_not_called()
    {
        _sessions.Next = new SessionCheck(SessionOutcome.Unavailable);
        using var gateway = GatewayHost.For(_api.Address, _sessions);
        using var client = GatewayHost.ClientOf(gateway);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", SessionCookie);

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
        Assert.Equal(0, _api.Requests);
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task What_the_session_check_wants_the_browser_to_get_comes_before_the_cookies_of_the_api(bool valid)
    {
        _sessions.Next = valid
            ? new SessionCheck(SessionOutcome.Valid, AccountId, ["session=renewed; path=/"])
            : new SessionCheck(SessionOutcome.Invalid, SetCookies: ["session=renewed; path=/"]);
        using var gateway = GatewayHost.For(_api.Address, _sessions);
        using var client = GatewayHost.ClientOf(gateway);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", SessionCookie);

        var response = await client.SendAsync(request);

        Assert.Equal(["session=renewed; path=/", "session=from-api; path=/; httponly"], response.Headers.GetValues("Set-Cookie"));
    }

    private async Task<Received> Get(string? cookie, string? authorization = null)
    {
        using var gateway = GatewayHost.For(_api.Address, _sessions);
        using var client = GatewayHost.ClientOf(gateway);
        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        if (cookie is not null)
        {
            request.Headers.Add("Cookie", cookie);
        }

        if (authorization is not null)
        {
            request.Headers.TryAddWithoutValidation("Authorization", authorization);
        }

        var response = await client.SendAsync(request);
        response.EnsureSuccessStatusCode();
        return (await response.Content.ReadFromJsonAsync<Received>())!;
    }

    private sealed class FakeSessions : ISessionValidator
    {
        private int _calls;

        public SessionCheck Next { get; set; } = new(SessionOutcome.Invalid);

        public string? LastCookie { get; private set; }

        public int Calls => _calls;

        public Task<SessionCheck> ValidateAsync(string cookieHeader, CancellationToken cancellationToken)
        {
            Interlocked.Increment(ref _calls);
            LastCookie = cookieHeader;
            return Task.FromResult(Next);
        }
    }
}
