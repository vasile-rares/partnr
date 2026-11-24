using System.Net.Http.Json;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Sessions;
using Partnr.Identity.Application.DTOs.Auth;
using Partnr.Identity.Infrastructure.Authentication;

namespace Partnr.Identity.IntegrationTests.Internal;

/// <summary>The gateway's gRPC question "is this session cookie good?", asked the way the gateway asks it.</summary>
[Collection(ApiCollection.Name)]
public sealed class SessionValidationTests(ApiFactory factory)
{
    [Fact]
    public async Task A_cookie_of_a_signed_in_account_is_valid_and_names_the_account()
    {
        var (cookie, accountId) = await SignIn();

        var reply = await Validate(cookie);

        Assert.True(reply.Valid);
        Assert.Equal(accountId.ToString(), reply.UserId);
    }

    [Fact]
    public async Task No_cookie_is_not_a_session()
    {
        var reply = await Validate(null);

        Assert.False(reply.Valid);
    }

    [Fact]
    public async Task A_made_up_cookie_is_not_a_session()
    {
        var reply = await Validate(".AspNetCore.Identity.Application=not-a-real-ticket");

        Assert.False(reply.Valid);
    }

    [Fact]
    public async Task A_cookie_stops_being_valid_when_the_account_changes_its_security_stamp()
    {
        var (cookie, accountId) = await SignIn();

        using (var scope = factory.Services.CreateScope())
        {
            var users = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
            await users.UpdateSecurityStampAsync((await users.FindByIdAsync(accountId.ToString()))!);
        }

        var reply = await Validate(cookie);

        Assert.False(reply.Valid);
    }

    private async Task<(string Cookie, Guid AccountId)> SignIn()
    {
        var client = factory.CreateClient();
        var response = await client.PostAsJsonAsync(
            "/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@example.com", password = "correct horse", fullName = "Test User" });
        response.EnsureSuccessStatusCode();

        var setCookie = Assert.Single(response.Headers.GetValues("Set-Cookie"), c => c.StartsWith(".AspNetCore.Identity.Application=", StringComparison.Ordinal));
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me");

        return (setCookie.Split(';')[0], me!.Id);
    }

    private async Task<ValidateReply> Validate(string? cookie)
    {
        using var http = factory.CreateDefaultClient(new ResponseVersionHandler());
        using var channel = GrpcChannel.ForAddress(http.BaseAddress!, new GrpcChannelOptions { HttpClient = http });

        var metadata = new Metadata();
        if (cookie is not null)
        {
            metadata.Add("cookie", cookie);
        }

        return await new SessionValidation.SessionValidationClient(channel).ValidateAsync(new ValidateRequest(), metadata);
    }

    // The test server answers with HTTP/1.1 as the version, which a gRPC client refuses.
    private sealed class ResponseVersionHandler : DelegatingHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            var response = await base.SendAsync(request, cancellationToken);
            response.Version = request.Version;
            return response;
        }
    }
}
