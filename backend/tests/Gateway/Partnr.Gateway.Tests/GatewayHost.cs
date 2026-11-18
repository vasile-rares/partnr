using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Gateway.Sessions;

namespace Partnr.Gateway.Tests;

internal static class GatewayHost
{
    // 32 bytes, base64; only ever used by these tests.
    public const string SigningKey = "dGVzdC1rZXktdGVzdC1rZXktdGVzdC1rZXktdGVzdCE=";

    public static WebApplicationFactory<Program> For(string apiAddress, ISessionValidator? sessions = null) =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseSetting("ReverseProxy:Clusters:workspaces:Destinations:api:Address", apiAddress);
            builder.UseSetting("InternalAuth:SigningKey", SigningKey);

            if (sessions is not null)
            {
                builder.ConfigureTestServices(services => services.AddSingleton(sessions));
            }
        });

    // The cookie jar stays off: the tests send and read the cookie headers themselves.
    public static HttpClient ClientOf(WebApplicationFactory<Program> gateway) =>
        gateway.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false, AllowAutoRedirect = false });
}
