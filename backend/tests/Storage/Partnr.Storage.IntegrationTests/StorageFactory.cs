using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Partnr.Storage.Application.Exceptions;
using Partnr.Storage.Application.Interfaces.Services;
using Partnr.Storage.Infrastructure.Persistence;
using Testcontainers.PostgreSql;

namespace Partnr.Storage.IntegrationTests;

/// <summary>Runs the real Storage service against a throwaway Postgres container (needs Docker), with the object store in memory.</summary>
public class StorageFactory : WebApplicationFactory<Program>, IAsyncLifetime
{
    private readonly PostgreSqlContainer postgres = new PostgreSqlBuilder("postgres:17").Build();

    public InMemoryBlobStore Blobs { get; } = new();

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
        await scope.ServiceProvider.GetRequiredService<StorageDbContext>().Database.MigrateAsync();
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
        builder.ConfigureServices(services => services.Replace(ServiceDescriptor.Singleton<IBlobStore>(Blobs)));
    }
}

/// <summary>Keeps the objects in memory. <c>S3BlobStoreTests</c> covers the real one.</summary>
public sealed class InMemoryBlobStore : IBlobStore
{
    private readonly ConcurrentDictionary<string, byte[]> _objects = new();

    public bool Contains(string key) => _objects.ContainsKey(key);

    public async Task PutAsync(string key, Stream content, string contentType, CancellationToken cancellationToken)
    {
        using var copy = new MemoryStream();
        await content.CopyToAsync(copy, cancellationToken);
        _objects[key] = copy.ToArray();
    }

    public Task<Stream> OpenReadAsync(string key, CancellationToken cancellationToken) =>
        _objects.TryGetValue(key, out var bytes)
            ? Task.FromResult<Stream>(new MemoryStream(bytes))
            : throw new NotFoundException("The file's content was not found.");

    public Task DeleteAsync(string key, CancellationToken cancellationToken)
    {
        _objects.TryRemove(key, out _);
        return Task.CompletedTask;
    }
}

[CollectionDefinition(Name)]
public sealed class ApiCollection : ICollectionFixture<StorageFactory>
{
    public const string Name = "Api";
}
