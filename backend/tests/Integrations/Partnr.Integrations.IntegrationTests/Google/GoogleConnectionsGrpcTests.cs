using System.Net.Http.Json;
using System.Web;
using Grpc.Core;
using Grpc.Net.Client;
using Microsoft.AspNetCore.Mvc.Testing;
using Partnr.Contracts.Integrations;
using Partnr.Integrations.Application.DTOs.Google;
using Partnr.Integrations.Application.Interfaces.Services;

namespace Partnr.Integrations.IntegrationTests.Google;

/// <summary>What the other services ask about a connection, asked the way they ask it (over gRPC, no session).</summary>
[Collection(ApiCollection.Name)]
public sealed class GoogleConnectionsGrpcTests(IntegrationsFactory factory)
{
    [Fact]
    public async Task AnAccountThatNeverConnected_IsNotConnected()
    {
        Assert.Equal(ConnectionState.NotConnected, await StateOf(Guid.NewGuid()));
    }

    [Fact]
    public async Task AnAccountThatConnected_IsConnected()
    {
        var userId = await Connected();

        Assert.Equal(ConnectionState.Connected, await StateOf(userId));
    }

    [Fact]
    public async Task AnAccountThatDisconnected_IsNotConnectedAnyMore()
    {
        var userId = await Connected();
        using var client = SignedIn(userId);
        (await client.DeleteAsync("/api/integrations/google")).EnsureSuccessStatusCode();

        Assert.Equal(ConnectionState.NotConnected, await StateOf(userId));
    }

    [Fact]
    public async Task ABadAccountId_IsRefused()
    {
        var error = await Assert.ThrowsAsync<RpcException>(() => Ask("not-a-guid"));

        Assert.Equal(StatusCode.InvalidArgument, error.StatusCode);
    }

    private async Task<Guid> Connected()
    {
        var userId = Guid.NewGuid();
        using var client = SignedIn(userId);
        var code = $"code-{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant("1//x", $"ana-{Guid.NewGuid():N}@gmail.example", true);
        var url = (await (await client.PostAsync("/api/integrations/google/connect", null)).Content.ReadFromJsonAsync<GoogleConnectUrlResponse>())!.Url;
        var state = HttpUtility.ParseQueryString(new Uri(url).Query)["state"]!;
        await client.GetAsync($"/api/integrations/google/callback?code={code}&state={Uri.EscapeDataString(state)}");

        return userId;
    }

    private async Task<ConnectionState> StateOf(Guid userId) => (await Ask(userId.ToString())).State;

    private async Task<GetStatusReply> Ask(string userId)
    {
        using var http = factory.CreateDefaultClient(new ResponseVersionHandler());
        using var channel = GrpcChannel.ForAddress(http.BaseAddress!, new GrpcChannelOptions { HttpClient = http });

        return await new GoogleConnections.GoogleConnectionsClient(channel).GetStatusAsync(new GetStatusRequest { UserId = userId });
    }

    private HttpClient SignedIn(Guid userId)
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        client.DefaultRequestHeaders.Authorization = new("Bearer", InternalTokens.Issue(userId.ToString()));

        return client;
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
