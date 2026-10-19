using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Testcontainers.PostgreSql;

namespace ClientPortal.IntegrationTests;

/// <summary>Runs the real API against a throwaway Postgres container (needs Docker).</summary>
public sealed class ApiFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();

    public FakeEmailSender Emails { get; } = new();

    public InMemoryFileStorage Files { get; } = new();

    public async Task InitializeAsync()
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
    }

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("ConnectionStrings:Postgres", postgres.GetConnectionString());
        builder.ConfigureServices(services =>
        {
            services.Replace(ServiceDescriptor.Singleton<IEmailSender>(Emails));
            services.Replace(ServiceDescriptor.Singleton<IFileStorage>(Files));
        });
    }
}
