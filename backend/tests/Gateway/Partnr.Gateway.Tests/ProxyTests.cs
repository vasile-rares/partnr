using System.Net;
using System.Net.Http.Json;

namespace Partnr.Gateway.Tests;

public sealed class ProxyTests : IAsyncLifetime
{
    private FakeApi _api = null!;

    public async Task InitializeAsync() => _api = await FakeApi.StartAsync();

    public async Task DisposeAsync() => await _api.DisposeAsync();

    [Fact]
    public async Task Forwards_method_path_query_body_and_cookie()
    {
        using var gateway = GatewayHost.For(_api.Address);
        using var client = GatewayHost.ClientOf(gateway);
        using var request = new HttpRequestMessage(HttpMethod.Post, "/api/workspaces/abc/projects?archived=false")
        {
            Content = new StringContent("hello"),
        };
        request.Headers.Add("Cookie", "session=from-browser");

        var response = await client.SendAsync(request);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var received = await response.Content.ReadFromJsonAsync<Received>();
        Assert.Equal(new Received("POST", "/api/workspaces/abc/projects?archived=false", "session=from-browser", 5, ""), received);
    }

    [Fact]
    public async Task Passes_the_cookie_the_api_sets_back_to_the_browser()
    {
        using var gateway = GatewayHost.For(_api.Address);
        using var client = GatewayHost.ClientOf(gateway);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Contains("session=from-api; path=/; httponly", response.Headers.GetValues("Set-Cookie"));
    }

    [Fact]
    public async Task Streams_a_body_as_large_as_the_biggest_file_upload()
    {
        using var gateway = GatewayHost.For(_api.Address);
        using var client = GatewayHost.ClientOf(gateway);
        var body = new byte[26 * 1024 * 1024];

        var response = await client.PostAsync("/api/workspaces/abc/projects/def/files", new ByteArrayContent(body));

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(body.Length, (await response.Content.ReadFromJsonAsync<Received>())!.Length);
    }

    [Fact]
    public async Task Serves_only_the_api_routes()
    {
        using var gateway = GatewayHost.For(_api.Address);
        using var client = GatewayHost.ClientOf(gateway);

        var response = await client.GetAsync("/login");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Theory]
    [InlineData("/scalar/v1")]
    [InlineData("/openapi/v1.json")]
    public async Task In_development_the_api_docs_are_reachable_with_the_gateways_own_address_as_the_host(string path)
    {
        using var gateway = GatewayHost.For(_api.Address);
        using var client = GatewayHost.ClientOf(gateway);

        var response = await client.GetAsync(path);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal(path, (await response.Content.ReadFromJsonAsync<Received>())!.PathAndQuery);
        Assert.Equal("localhost", _api.LastHost);
    }

    [Fact]
    public async Task Sends_the_integrations_routes_to_the_integrations_service_and_everything_else_to_the_api()
    {
        await using var integrations = await FakeApi.StartAsync();
        using var gateway = GatewayHost.For(_api.Address, integrationsAddress: integrations.Address);
        using var client = GatewayHost.ClientOf(gateway);

        await client.GetAsync("/api/integrations/google");
        await client.GetAsync("/api/integrations/google/callback?code=c&state=s");
        await client.GetAsync("/api/workspaces/abc/meetings");
        await client.GetAsync("/api/auth/me");

        Assert.Equal(2, integrations.Requests);
        Assert.Equal(2, _api.Requests);
    }

    [Fact]
    public async Task Answers_its_own_health_check_without_asking_the_api()
    {
        using var gateway = GatewayHost.For("http://127.0.0.1:1");
        using var client = GatewayHost.ClientOf(gateway);

        var response = await client.GetAsync("/health");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Answers_bad_gateway_when_the_api_is_down()
    {
        using var gateway = GatewayHost.For("http://127.0.0.1:1");
        using var client = GatewayHost.ClientOf(gateway);

        var response = await client.GetAsync("/api/auth/me");

        Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
    }
}
