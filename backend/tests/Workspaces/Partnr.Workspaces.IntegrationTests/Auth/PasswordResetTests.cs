using System.Net;
using System.Net.Http.Json;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Contracts.Events;

namespace Partnr.Workspaces.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed class PasswordResetTests(ApiFactory factory)
{
    private const string Password = "correct horse";
    private const string NewPassword = "battery staple";

    [Fact]
    public async Task ForgotPassword_EmailsALink_ThatSetsANewPassword_AndSignsInThisDevice()
    {
        var email = await Register();
        var device = factory.CreateClient();

        var asked = await device.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = (await EmailTo(email)).Token;

        Assert.Equal(HttpStatusCode.NoContent, asked.StatusCode);
        var preview = await device.GetFromJsonAsync<PasswordResetPreviewResponse>($"/api/auth/reset-password/{token}");
        Assert.Equal(email, preview!.Email);

        var reset = await device.PostAsJsonAsync("/api/auth/reset-password", new { token, password = NewPassword });

        Assert.Equal(HttpStatusCode.OK, reset.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await device.GetAsync("/api/auth/me")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await Login(email, Password)).StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await Login(email, NewPassword)).StatusCode);
    }

    [Fact]
    public async Task Resetting_SignsOutEveryOtherSession()
    {
        var email = await Register();
        var laptop = factory.CreateClient();
        Assert.Equal(HttpStatusCode.OK, (await laptop.PostAsJsonAsync("/api/auth/login", new { email, password = Password })).StatusCode);

        await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = (await EmailTo(email)).Token;
        await factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new { token, password = NewPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ALink_WorksOnce()
    {
        var email = await Register();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = (await EmailTo(email)).Token;
        await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = NewPassword });

        var again = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "something else" });

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(HttpStatusCode.Conflict, (await client.GetAsync($"/api/auth/reset-password/{token}")).StatusCode);
    }

    [Fact]
    public async Task AnUnknownEmail_GetsTheSameAnswer_AndNoEmail()
    {
        var nobody = NewEmail();

        var asked = await factory.CreateClient().PostAsJsonAsync("/api/auth/forgot-password", new { email = nobody });

        Assert.Equal(HttpStatusCode.NoContent, asked.StatusCode);
        Assert.Empty(factory.Recorded.ResetsTo(nobody));
    }

    [Fact]
    public async Task AskingAgainRightAway_SendsNothingMore()
    {
        var email = await Register();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        await EmailTo(email);
        var second = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(factory.Recorded.ResetsTo(email));
    }

    [Fact]
    public async Task AShortPassword_IsRefused_AndTheLinkStillWorks()
    {
        var email = await Register();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = (await EmailTo(email)).Token;

        var tooShort = await client.PostAsJsonAsync("/api/auth/reset-password", new { token, password = "short" });

        Assert.Equal(HttpStatusCode.BadRequest, tooShort.StatusCode);
        Assert.Equal(HttpStatusCode.OK, (await client.GetAsync($"/api/auth/reset-password/{token}")).StatusCode);
    }

    [Theory]
    [InlineData("not-one-of-ours")]
    [InlineData("aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa")]
    public async Task ALinkThatIsNotOurs_IsNotFound(string token)
    {
        var client = factory.CreateClient();

        Assert.Equal(HttpStatusCode.NotFound, (await client.GetAsync($"/api/auth/reset-password/{token}")).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsJsonAsync("/api/auth/reset-password", new { token = "not-one-of-ours", password = NewPassword })).StatusCode);
    }

    private async Task<string> Register()
    {
        var email = NewEmail();
        var registered = await factory.CreateClient().PostAsJsonAsync("/api/auth/register", new { email, password = Password, fullName = "Ana Pop" });
        registered.EnsureSuccessStatusCode();

        return email;
    }

    private Task<HttpResponseMessage> Login(string email, string password) =>
        factory.CreateClient().PostAsJsonAsync("/api/auth/login", new { email, password });

    private Task<PasswordResetRequested> EmailTo(string address) =>
        Task.FromResult(Assert.Single(factory.Recorded.ResetsTo(address)));

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";
}
