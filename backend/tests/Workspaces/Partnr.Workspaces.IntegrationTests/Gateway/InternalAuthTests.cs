using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts;
using Partnr.Workspaces.Application.DTOs.Auth;

namespace Partnr.Workspaces.IntegrationTests.Gateway;

/// <summary>The API as it is behind the gateway: no gateway emulator in front, so only the gateway's JWT proves who is calling.</summary>
[Collection(ApiCollection.Name)]
public sealed class InternalAuthTests(ApiFactory factory)
{
    private const string OtherKey = "b3RoZXItc2lnbmluZy1rZXktMzItYnl0ZXMtbG9uZyE=";

    [Fact]
    public async Task A_session_cookie_alone_is_not_enough()
    {
        using var api = Api();
        using var client = api.CreateClient();
        await Register(client);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task The_gateways_jwt_is_enough_and_names_the_account()
    {
        var accountId = await RegisterAccount();
        using var api = Api();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", InternalTokens.Issue(accountId.ToString()));

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(accountId, (await response.Content.ReadFromJsonAsync<CurrentUserResponse>())!.Id);
    }

    [Fact]
    public async Task A_route_open_to_anyone_still_works_without_a_jwt()
    {
        using var api = Api();
        using var client = api.CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@example.com", password = "correct horse", fullName = "Test User" });

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        Assert.Contains(response.Headers.GetValues("Set-Cookie"), cookie => cookie.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
    }

    [Theory]
    [InlineData("other key")]
    [InlineData("other issuer")]
    [InlineData("other audience")]
    [InlineData("expired")]
    public async Task A_token_the_gateway_did_not_make_is_refused(string flaw)
    {
        var accountId = await RegisterAccount();
        var token = flaw switch
        {
            "other key" => InternalTokens.Issue(accountId.ToString(), signingKey: OtherKey),
            "other issuer" => InternalTokens.Issue(accountId.ToString(), issuer: "someone-else"),
            "other audience" => InternalTokens.Issue(accountId.ToString(), audience: "someone-else"),
            _ => InternalTokens.Issue(accountId.ToString(), lifetime: TimeSpan.FromMinutes(-11)),
        };
        using var api = Api();
        using var client = api.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    private WebApplicationFactory<Program> Api() =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            foreach (var emulator in services.Where(service => service.ImplementationType == typeof(GatewayEmulatorStartupFilter)).ToList())
            {
                services.Remove(emulator);
            }
        }));

    // Through the emulator, which is what a browser's cookie goes through in these tests.
    private async Task<Guid> RegisterAccount()
    {
        var client = factory.CreateClient();
        await Register(client);
        return (await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!.Id;
    }

    private static async Task Register(HttpClient client)
    {
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@example.com", password = "correct horse", fullName = "Test User" });
        response.EnsureSuccessStatusCode();
    }
}
