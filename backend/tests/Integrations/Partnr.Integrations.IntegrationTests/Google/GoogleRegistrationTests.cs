using Partnr.Integrations.Application;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Partnr.Integrations.IntegrationTests.Google;

// No container: only checks that everything the Google feature needs is registered (a missing line otherwise breaks on the first request).
public class GoogleRegistrationTests
{
    [Fact]
    public async Task TheConnectionService_AndItsParts_CanBeBuilt()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused",
            ["App:FrontendUrl"] = "http://localhost:3000",
            ["Google:ClientId"] = "id",
            ["Google:ClientSecret"] = "secret",
            ["Google:RedirectUri"] = "http://localhost:3000/api/integrations/google/callback",
            ["Google:EncryptionKey"] = "byS15QxyadAsWI5yIoIgUlIwfAYH17sJrwkWib43D+0=",
        }).Build();

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment())
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();

        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGoogleConnectionService>());
        Assert.True(scope.ServiceProvider.GetRequiredService<IGoogleOAuthClient>().IsConfigured);
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<ISecretProtector>());
    }

    [Fact]
    public async Task TheFakeGoogle_NeedsNoCredentials_AndSkipsTheConsentScreen()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused",
            ["App:FrontendUrl"] = "http://localhost:3100",
            ["Google:Fake"] = "true",
            ["Google:RedirectUri"] = "http://localhost:3100/api/integrations/google/callback",
            ["Google:EncryptionKey"] = "byS15QxyadAsWI5yIoIgUlIwfAYH17sJrwkWib43D+0=",
        }).Build();

        await using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment())
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var google = scope.ServiceProvider.GetRequiredService<IGoogleOAuthClient>();

        Assert.True(google.IsConfigured);
        Assert.StartsWith("http://localhost:3100/api/integrations/google/callback?code=fake-ok&state=", google.AuthorizationUrl("s"));
    }
}
