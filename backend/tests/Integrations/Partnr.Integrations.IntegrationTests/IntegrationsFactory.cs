using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Partnr.Integrations.IntegrationTests;

/// <summary>Runs the real Integrations service against a throwaway Postgres container (needs Docker).</summary>
public class IntegrationsFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();

    public FakeGoogleOAuthClient Google { get; } = new();

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
        await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>().Database.MigrateAsync();
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
        // No broker unless a test brings one: the development settings point at a RabbitMQ that may not be running.
        builder.UseSetting("Events:RabbitMqUri", "");
        builder.UseSetting("ConnectionStrings:Postgres", postgres.GetConnectionString());
        builder.UseSetting("InternalAuth:SigningKey", InternalTokens.SigningKey);
        builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IGoogleOAuthClient>(Google)));
    }
}
