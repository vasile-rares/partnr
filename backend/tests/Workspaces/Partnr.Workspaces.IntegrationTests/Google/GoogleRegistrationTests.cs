using Partnr.Workspaces.Application;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Infrastructure;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Partnr.Workspaces.IntegrationTests.Google;

// No container: only checks that everything the Google feature needs is registered (a missing line otherwise breaks on the first request).
public class GoogleRegistrationTests
{
    [Fact]
    public void TheConnectionService_AndItsParts_CanBeBuilt()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused",
            ["App:FrontendUrl"] = "http://localhost:3000",
            ["Email:From"] = "Partnr <no-reply@partnr.local>",
            ["Email:SmtpHost"] = "localhost",
            ["Storage:Bucket"] = "unused",
            ["Storage:AccessKey"] = "unused",
            ["Storage:SecretKey"] = "unused",
            ["Google:ClientId"] = "id",
            ["Google:ClientSecret"] = "secret",
            ["Google:RedirectUri"] = "http://localhost:3000/api/integrations/google/callback",
            ["Google:EncryptionKey"] = "byS15QxyadAsWI5yIoIgUlIwfAYH17sJrwkWib43D+0=",
        }).Build();

        using var provider = new ServiceCollection()
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
    public void TheFakeGoogle_NeedsNoCredentials_AndSkipsTheConsentScreen()
    {
        var configuration = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Postgres"] = "Host=localhost;Database=unused",
            ["App:FrontendUrl"] = "http://localhost:3100",
            ["Email:From"] = "Partnr <no-reply@partnr.local>",
            ["Email:SmtpHost"] = "localhost",
            ["Storage:Bucket"] = "unused",
            ["Storage:AccessKey"] = "unused",
            ["Storage:SecretKey"] = "unused",
            ["Google:Fake"] = "true",
            ["Google:RedirectUri"] = "http://localhost:3100/api/integrations/google/callback",
            ["Google:EncryptionKey"] = "byS15QxyadAsWI5yIoIgUlIwfAYH17sJrwkWib43D+0=",
        }).Build();

        using var provider = new ServiceCollection()
            .AddLogging()
            .AddSingleton<IHostEnvironment>(new TestHostEnvironment())
            .AddApplication()
            .AddInfrastructure(configuration)
            .BuildServiceProvider(validateScopes: true);
        using var scope = provider.CreateScope();
        var google = scope.ServiceProvider.GetRequiredService<IGoogleOAuthClient>();

        Assert.True(google.IsConfigured);
        Assert.StartsWith("http://localhost:3100/api/integrations/google/callback?code=fake-ok&state=", google.AuthorizationUrl("s"));
        Assert.NotNull(scope.ServiceProvider.GetRequiredService<IGoogleCalendarClient>());
    }
}
