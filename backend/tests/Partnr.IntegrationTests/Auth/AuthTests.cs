using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Partnr.Application.DTOs.Auth;
using Partnr.Infrastructure.Persistence;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class AuthTests(ApiFactory factory)
{
    private const string Password = "correct horse";

    [Fact]
    public async Task Register_CreatesAccountAndSignsIn()
    {
        var client = factory.CreateClient();
        var email = NewEmail();

        var response = await Register(client, email);

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        var me = await client.GetAsync("/api/auth/me");
        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var user = await me.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(email, user!.Email);
        Assert.Equal("Test User", user.FullName);
    }

    [Fact]
    public async Task Register_SetsHttpOnlyCookie()
    {
        var response = await Register(factory.CreateClient(), NewEmail());

        var cookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application="));
        Assert.Contains("httponly", cookie, StringComparison.OrdinalIgnoreCase);
        Assert.Contains("samesite=lax", cookie, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Register_WithSameEmailInAnotherCase_ReturnsConflict()
    {
        var email = NewEmail();
        await Register(factory.CreateClient(), email);

        var response = await Register(factory.CreateClient(), email.ToUpperInvariant());

        Assert.Equal(HttpStatusCode.Conflict, response.StatusCode);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var response = await Register(factory.CreateClient(), NewEmail(), password: "short");

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Me_WithoutLogin_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithCorrectPassword_SignsIn()
    {
        var email = NewEmail();
        await Register(factory.CreateClient(), email);
        var client = factory.CreateClient();

        var login = await Login(client, email, Password);

        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPasswordOrUnknownEmail_ReturnsUnauthorizedWithSameMessage()
    {
        var email = NewEmail();
        await Register(factory.CreateClient(), email);
        var client = factory.CreateClient();

        var wrongPassword = await Login(client, email, "wrong password");
        var unknownEmail = await Login(client, NewEmail(), "wrong password");

        Assert.Equal(HttpStatusCode.Unauthorized, wrongPassword.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, unknownEmail.StatusCode);
        // Compare the message only: the full body also carries a per-request traceId.
        Assert.Equal(await ReadDetail(wrongPassword), await ReadDetail(unknownEmail));
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task Login_AfterFiveWrongPasswords_IsLockedOutEvenWithCorrectPassword()
    {
        var email = NewEmail();
        await Register(factory.CreateClient(), email);
        var client = factory.CreateClient();

        for (var attempt = 0; attempt < 5; attempt++)
        {
            await Login(client, email, "wrong password");
        }

        var response = await Login(client, email, Password);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Logout_EndsTheSession()
    {
        var client = factory.CreateClient();
        await Register(client, NewEmail());

        var logout = await client.PostAsync("/api/auth/logout", content: null);

        Assert.Equal(HttpStatusCode.NoContent, logout.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await client.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutLogin_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().GetAsync("/api/workspaces");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task ProtectedEndpoint_WithLogin_IsReachable()
    {
        var client = factory.CreateClient();
        await Register(client, NewEmail());

        var response = await client.GetAsync("/api/workspaces");

        // A 200 (an empty list for a new account) can only come from the endpoint itself, so authorization let the request through.
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
    }

    [Fact]
    public async Task UpdateProfile_SavesTheDetails_AndMeReturnsThem()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await Register(client, email);

        var response = await client.PutAsJsonAsync(
            "/api/auth/me",
            new { fullName = " Ana Popescu ", phone = "+40 721 000 000", company = "Acme SRL" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal("Ana Popescu", me!.FullName);
        Assert.Equal("+40 721 000 000", me.Phone);
        Assert.Equal("Acme SRL", me.Company);
        Assert.Equal(email, me.Email);
    }

    [Fact]
    public async Task UpdateProfile_EmptyOptionalDetails_AreCleared()
    {
        var client = factory.CreateClient();
        await Register(client, NewEmail());
        await client.PutAsJsonAsync("/api/auth/me", new { fullName = "Ana", phone = "0700", company = "Acme" });

        await client.PutAsJsonAsync("/api/auth/me", new { fullName = "Ana", phone = "", company = (string?)null });

        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Null(me!.Phone);
        Assert.Null(me.Company);
    }

    [Fact]
    public async Task UpdateProfile_IgnoresAnEmailInTheRequest()
    {
        var client = factory.CreateClient();
        var email = NewEmail();
        await Register(client, email);

        var response = await client.PutAsJsonAsync("/api/auth/me", new { fullName = "Ana", email = NewEmail() });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal(email, me!.Email);
    }

    [Fact]
    public async Task UpdateProfile_WithBlankName_ReturnsBadRequest()
    {
        var client = factory.CreateClient();
        await Register(client, NewEmail());

        var response = await client.PutAsJsonAsync("/api/auth/me", new { fullName = "  " });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");
        Assert.Equal("Test User", me!.FullName);
    }

    [Fact]
    public async Task UpdateProfile_WithoutLogin_ReturnsUnauthorized()
    {
        var response = await factory.CreateClient().PutAsJsonAsync("/api/auth/me", new { fullName = "Ana" });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Cookie_OfADeletedAccount_IsSignedOutOnTheNextRequest_EverywhereNotOnlyOnMe()
    {
        var client = factory.CreateClient();
        var registered = await Register(client, NewEmail());
        var user = await registered.Content.ReadFromJsonAsync<CurrentUserResponse>();
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync("/api/workspaces")).StatusCode);

        using (var scope = factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await db.Database.ExecuteSqlAsync($"DELETE FROM users WHERE \"Id\" = {user!.Id}");
        }

        // Without a check on every request, /api/workspaces answered 200 with an empty list and /api/auth/me threw.
        var workspaces = await client.GetAsync("/api/workspaces");
        var me = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.Unauthorized, workspaces.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
    }

    [Fact]
    public async Task AnAuthenticatedRequest_DoesNotReissueTheSessionCookie_SoALateResponseCannotReviveASignedOutSession()
    {
        // A request still in flight when someone signs out answers after the sign-out. If that answer carried a fresh cookie, the browser would keep it.
        using var client = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var registered = await Register(client, NewEmail());
        var session = Assert.Single(registered.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application=")).Split(';')[0];

        using var request = new HttpRequestMessage(HttpMethod.Get, "/api/auth/me");
        request.Headers.Add("Cookie", session);
        var me = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, me.StatusCode);
        var setCookies = me.Headers.TryGetValues("Set-Cookie", out var values) ? values : [];
        Assert.DoesNotContain(setCookies, c => c.StartsWith(".AspNetCore.Identity.Application="));
    }

    private static async Task<string?> ReadDetail(HttpResponseMessage response)
    {
        using var body = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return body.RootElement.GetProperty("detail").GetString();
    }

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    private static Task<HttpResponseMessage> Register(HttpClient client, string email, string password = Password) =>
        client.PostAsJsonAsync("/api/auth/register", new { email, password, fullName = "Test User" });

    private static Task<HttpResponseMessage> Login(HttpClient client, string email, string password) =>
        client.PostAsJsonAsync("/api/auth/login", new { email, password });
}
