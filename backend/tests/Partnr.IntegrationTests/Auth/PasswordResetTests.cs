using System.Net;
using System.Net.Http.Json;
using System.Text.RegularExpressions;
using Partnr.Application.DTOs.Auth;
using Partnr.Application.DTOs.Email;

namespace Partnr.IntegrationTests.Auth;

[Collection(ApiCollection.Name)]
public sealed partial class PasswordResetTests(ApiFactory factory)
{
    private const string Password = "correct horse";
    private const string NewPassword = "battery staple";

    [Fact]
    public async Task ForgotPassword_EmailsALink_ThatSetsANewPassword_AndSignsInThisDevice()
    {
        var email = await Register();
        var device = factory.CreateClient();

        var asked = await device.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = TokenIn(await EmailTo(email));

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
        var token = TokenIn(await EmailTo(email));
        await factory.CreateClient().PostAsJsonAsync("/api/auth/reset-password", new { token, password = NewPassword });

        Assert.Equal(HttpStatusCode.Unauthorized, (await laptop.GetAsync("/api/auth/me")).StatusCode);
    }

    [Fact]
    public async Task ALink_WorksOnce()
    {
        var email = await Register();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = TokenIn(await EmailTo(email));
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
        await Task.Delay(300); // the queue would have sent it by now
        Assert.Empty(factory.Emails.SentTo(nobody));
    }

    [Fact]
    public async Task AskingAgainRightAway_SendsNothingMore()
    {
        var email = await Register();
        var client = factory.CreateClient();

        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        await EmailTo(email);
        var second = await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        await Task.Delay(300);

        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);
        Assert.Single(factory.Emails.SentTo(email));
    }

    [Fact]
    public async Task AShortPassword_IsRefused_AndTheLinkStillWorks()
    {
        var email = await Register();
        var client = factory.CreateClient();
        await client.PostAsJsonAsync("/api/auth/forgot-password", new { email });
        var token = TokenIn(await EmailTo(email));

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

    // The email leaves from a background queue, after the request answered.
    private async Task<EmailMessage> EmailTo(string address)
    {
        var deadline = DateTime.UtcNow.AddSeconds(10);
        while (true)
        {
            var sent = factory.Emails.SentTo(address);
            if (sent.Count > 0 || DateTime.UtcNow > deadline)
            {
                return Assert.Single(sent);
            }

            await Task.Delay(25);
        }
    }

    private static string TokenIn(EmailMessage email) => ResetLink().Match(email.TextBody).Groups[1].Value;

    private static string NewEmail() => $"user-{Guid.NewGuid():N}@example.com";

    [GeneratedRegex("/reset-password/([A-Za-z0-9_-]+)")]
    private static partial Regex ResetLink();
}
