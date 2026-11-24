using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Partnr.Identity.Application.Interfaces.Services;
using Partnr.Identity.Infrastructure.Persistence;
using Partnr.Identity.IntegrationTests.Gateway;
using Testcontainers.PostgreSql;

namespace Partnr.Identity.IntegrationTests;

/// <summary>Runs the real Identity service against a throwaway Postgres container (needs Docker).</summary>
public class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();

    public RecordedEvents Recorded { get; } = new();

    public virtual async Task InitializeAsync()
    {
        await postgres.StartAsync();

        // The service also reads user secrets in Development, so make sure we are not talking to the developer's own database.
        var connectionString = Services.GetRequiredService<IConfiguration>().GetConnectionString("Postgres");
        if (connectionString != postgres.GetConnectionString())
        {
            throw new InvalidOperationException("The service is not using the test container's database; refusing to run the tests.");
        }

        using var scope = Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }

    // xunit v2's IAsyncLifetime returns Task, which clashes with WebApplicationFactory's ValueTask DisposeAsync, hence the explicit implementation.
    async Task IAsyncLifetime.DisposeAsync()
    {
        await base.DisposeAsync();
        await postgres.DisposeAsync();
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        // No broker: the development settings point at a RabbitMQ that may not be running.
        builder.UseSetting("Events:RabbitMqUri", "");
        builder.UseSetting("ConnectionStrings:Postgres", postgres.GetConnectionString());
        builder.UseSetting("InternalAuth:SigningKey", InternalTokens.SigningKey);
        builder.ConfigureServices(services =>
        {
            // The service only accepts the gateway's JWT; this turns the cookie the tests sign in with into one, like the gateway does.
            services.AddSingleton<IStartupFilter, GatewayEmulatorStartupFilter>();
            var publisher = services.Single(d => d.ServiceType == typeof(IEventPublisher)).ImplementationType!;
            services.Replace(ServiceDescriptor.Scoped<IEventPublisher>(sp =>
                new RecordingEventPublisher((IEventPublisher)ActivatorUtilities.CreateInstance(sp, publisher), Recorded)));
        });
    }
}
