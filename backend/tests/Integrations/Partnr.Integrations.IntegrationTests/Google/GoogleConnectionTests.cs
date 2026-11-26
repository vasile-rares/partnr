using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using Partnr.Integrations.Application.DTOs.Google;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.Integrations.IntegrationTests.Google;

[Collection(ApiCollection.Name)]
public sealed class GoogleConnectionTests(IntegrationsFactory factory)
{
    [Fact]
    public async Task EveryRoute_NeedsASignedInAccount()
    {
        var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/integrations/google")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/integrations/google/connect", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/integrations/google/callback?code=c&state=s")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.DeleteAsync("/api/integrations/google")).StatusCode);
    }

    [Fact]
    public async Task Connecting_GoesThroughGoogleAndBack_ThenDisconnectingRevokesIt()
    {
        var (client, _) = SignedIn();
        Assert.Equal(new GoogleConnectionResponse(true, "NotConnected", null), await Status(client));

        var code = $"code-{Guid.NewGuid():N}";
        var email = $"ana-{Guid.NewGuid():N}@gmail.example";
        var token = $"1//{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant(token, email, true);
        var back = await client.GetAsync($"/api/integrations/google/callback?code={code}&state={Uri.EscapeDataString(await NewState(client))}");

        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        Assert.EndsWith("/account?google=connected", back.Headers.Location!.ToString());
        Assert.Equal(new GoogleConnectionResponse(true, "Connected", email), await Status(client));

        // The token is in the database, but only in protected form.
        using (var scope = factory.Services.CreateScope())
        {
            var stored = await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>()
                .GoogleConnections.SingleAsync(c => c.GoogleEmail == email);
            Assert.DoesNotContain(token, stored.ProtectedRefreshToken);
        }

        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/integrations/google")).StatusCode);
        Assert.Equal("NotConnected", (await Status(client)).Status);
        Assert.Contains(token, factory.Google.Revoked);
        Assert.Equal(HttpStatusCode.NoContent, (await client.DeleteAsync("/api/integrations/google")).StatusCode);
    }

    [Fact]
    public async Task ComingBackFromGoogle_EndsOnThePageTheConnectionStartedFrom()
    {
        var (client, _) = SignedIn();
        var code = $"code-{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant($"1//{Guid.NewGuid():N}", $"ana-{Guid.NewGuid():N}@gmail.example", true);
        var connect = await client.PostAsJsonAsync("/api/integrations/google/connect", new { returnTo = "/w/some-workspace/calendar" });
        var url = (await connect.Content.ReadFromJsonAsync<GoogleConnectUrlResponse>())!.Url;
        var state = HttpUtility.ParseQueryString(new Uri(url).Query)["state"]!;

        var back = await client.GetAsync($"/api/integrations/google/callback?code={code}&state={Uri.EscapeDataString(state)}");

        Assert.Equal(HttpStatusCode.Redirect, back.StatusCode);
        Assert.EndsWith("/w/some-workspace/calendar?google=connected", back.Headers.Location!.ToString());
    }

    [Theory]
    [InlineData("//evil.example")]
    [InlineData("https://evil.example/x")]
    [InlineData("/calendar?next=1")]
    public async Task APathThatIsNotOfTheWebApp_IsRefused(string returnTo)
    {
        var (client, _) = SignedIn();

        Assert.Equal(HttpStatusCode.BadRequest, (await client.PostAsJsonAsync("/api/integrations/google/connect", new { returnTo })).StatusCode);
    }

    [Fact]
    public async Task ComingBackWithoutAGoodState_ConnectsNothing()
    {
        var (client, _) = SignedIn();
        var code = $"code-{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant("1//x", "ana@gmail.example", true);

        var forged = await client.GetAsync($"/api/integrations/google/callback?code={code}&state=forged");

        Assert.EndsWith("/account?google=error", forged.Headers.Location!.ToString());
        Assert.Equal("NotConnected", (await Status(client)).Status);
    }

    [Fact]
    public async Task AStateMadeForAnotherAccount_ConnectsNothing()
    {
        var (victim, _) = SignedIn();
        var (attacker, _) = SignedIn();
        var code = $"code-{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant("1//x", "attacker@gmail.example", true);

        var back = await attacker.GetAsync($"/api/integrations/google/callback?code={code}&state={Uri.EscapeDataString(await NewState(victim))}");

        Assert.EndsWith("/account?google=error", back.Headers.Location!.ToString());
        Assert.Equal("NotConnected", (await Status(attacker)).Status);
        Assert.Equal("NotConnected", (await Status(victim)).Status);
    }

    [Fact]
    public async Task WhenThePersonSaysNoOnGoogle_TheyComeBackDenied()
    {
        var (client, _) = SignedIn();

        var back = await client.GetAsync($"/api/integrations/google/callback?error=access_denied&state={Uri.EscapeDataString(await NewState(client))}");

        Assert.EndsWith("/account?google=denied", back.Headers.Location!.ToString());
        Assert.Equal("NotConnected", (await Status(client)).Status);
    }

    [Fact]
    public async Task WhenTheCalendarWasNotGranted_TheyComeBackWithoutTheAccess()
    {
        var (client, _) = SignedIn();
        var code = $"code-{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant("1//x", "ana@gmail.example", false);

        var back = await client.GetAsync($"/api/integrations/google/callback?code={code}&state={Uri.EscapeDataString(await NewState(client))}");

        Assert.EndsWith("/account?google=permission", back.Headers.Location!.ToString());
        Assert.Equal("NotConnected", (await Status(client)).Status);
    }

    [Fact]
    public async Task EachAccountHasItsOwnConnection()
    {
        var (ana, _) = SignedIn();
        var (mihai, _) = SignedIn();
        var code = $"code-{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant("1//x", $"ana-{Guid.NewGuid():N}@gmail.example", true);
        await ana.GetAsync($"/api/integrations/google/callback?code={code}&state={Uri.EscapeDataString(await NewState(ana))}");

        Assert.Equal("Connected", (await Status(ana)).Status);
        Assert.Equal("NotConnected", (await Status(mihai)).Status);
    }

    private static async Task<GoogleConnectionResponse> Status(HttpClient client) =>
        (await client.GetFromJsonAsync<GoogleConnectionResponse>("/api/integrations/google"))!;

    private static async Task<string> NewState(HttpClient client)
    {
        var url = (await (await client.PostAsync("/api/integrations/google/connect", null)).Content.ReadFromJsonAsync<GoogleConnectUrlResponse>())!.Url;

        return HttpUtility.ParseQueryString(new Uri(url).Query)["state"]!;
    }

    // Redirects are not followed: the callback ends on a page of the web app, which is not running here. The JWT is what the gateway would add.
    private (HttpClient Client, Guid UserId) SignedIn()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var userId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", InternalTokens.Issue(userId.ToString()));

        return (client, userId);
    }
}
