using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.IntegrationTests.Gateway;
using Partnr.Workspaces.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace Partnr.Workspaces.IntegrationTests;

/// <summary>Runs the real API against a throwaway Postgres container (needs Docker).</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();

    public FakeEmailSender Emails { get; } = new();

    public InMemoryFileStorage Files { get; } = new();

    public FakeGoogleConnectionStatus Google { get; } = new();

    public virtual async Task InitializeAsync()
    {
        await postgres.StartAsync();

        // The Api also reads user secrets in Development, so make sure we are not talking to the developer's own database.
        var connectionString = Services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres");
        if (connectionString != postgres.GetConnectionString())
        {
            throw new InvalidOperationException("The API is not using the test container's database; refusing to run the tests.");
        }

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    // xunit v2's IAsyncLifetime returns Task, which clashes with WebApplicationFactory's ValueTask DisposeAsync, hence the explicit implementation.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
        await DisposeContainersAsync();
    }

    protected virtual Task DisposeContainersAsync() => Task.CompletedTask;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // No broker unless a test brings one (BrokerApiFactory): the development settings point at a RabbitMQ that may not be running.
        builder.UseSetting("Events:RabbitMqUri", "");
        builder.UseSetting("ConnectionStrings:Postgres", postgres.GetConnectionString());

        // Tests run the email service themselves, so what they see does not depend on a timer.
        builder.UseSetting("Notifications:EmailWorkerEnabled", "false");
        builder.UseSetting("Services:Integrations:GrpcAddress", "http://localhost:1");
        builder.UseSetting("InternalAuth:SigningKey", InternalTokens.SigningKey);
        builder.ConfigureServices(services =>
        {
            // The API only accepts the gateway's JWT; this turns the cookie the tests sign in with into one, like the gateway does.
            services.AddSingleton<IStartupFilter, GatewayEmulatorStartupFilter>();
            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails));
            services.Replace(ServiceDescriptor.Singleton<IFileStorage>(Files));
            services.Replace(ServiceDescriptor.Singleton<IGoogleConnectionStatus>(Google));
        });
    }
}
