using System.Text;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.IntegrationTests.Storage;

/// <summary>Checks the real S3 adapter against a throwaway RustFS container.</summary>
public sealed class S3FileStorageTests : IAsyncLifetime
{
    private const int S3Port = 9000;
    private const string AccessKey = "test-access";
    private const string SecretKey = "test-secret-key";

    private readonly IContainer rustfs = new ContainerBuilder("rustfs/rustfs:1.0.3")
        .WithEnvironment("RUSTFS_ACCESS_KEY", AccessKey)
        .WithEnvironment("RUSTFS_SECRET_KEY", SecretKey)
        .WithPortBinding(S3Port, assignRandomHostPort: true)
        // Any answer under 500 means the S3 endpoint is up; an unsigned request is refused with 403.
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r
            .ForPort(S3Port)
            .ForStatusCodeMatching(code => (int)code < 500)))
        .Build();

    public Task InitializeAsync() => rustfs.StartAsync();

    public Task DisposeAsync() => rustfs.DisposeAsync().AsTask();

    [Fact]
    public async Task SaveThenOpen_ReturnsTheSameBytes_AndCreatesTheBucket()
    {
        var storage = BuildStorage();
        var key = $"{Guid.NewGuid()}/{Guid.NewGuid()}/{Guid.NewGuid()}";
        var bytes = Encoding.UTF8.GetBytes("hello from a deliverable");

        await storage.SaveAsync(key, new MemoryStream(bytes), "application/pdf", CancellationToken.None);

        await using var read = await storage.OpenReadAsync(key, CancellationToken.None);
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task Open_ABigFile_ReturnsItWhole()
    {
        var storage = BuildStorage();
        var key = $"big/{Guid.NewGuid()}";
        var bytes = new byte[8 * 1024 * 1024];
        Random.Shared.NextBytes(bytes);

        await storage.SaveAsync(key, new MemoryStream(bytes), "application/zip", CancellationToken.None);

        await using var read = await storage.OpenReadAsync(key, CancellationToken.None);
        using var copy = new MemoryStream();
        await read.CopyToAsync(copy);
        Assert.Equal(bytes, copy.ToArray());
    }

    [Fact]
    public async Task Open_AMissingKey_ThrowsNotFound()
    {
        var storage = BuildStorage();

        await Assert.ThrowsAsync<NotFoundException>(() => storage.OpenReadAsync($"missing/{Guid.NewGuid()}", CancellationToken.None));
    }

    [Fact]
    public async Task Delete_RemovesTheObject_AndNeverThrowsForAMissingOne()
    {
        var storage = BuildStorage();
        var key = $"to-delete/{Guid.NewGuid()}";
        await storage.SaveAsync(key, new MemoryStream([1, 2, 3]), "application/zip", CancellationToken.None);

        await storage.DeleteAsync(key, CancellationToken.None);
        await storage.DeleteAsync(key, CancellationToken.None);

        await Assert.ThrowsAsync<NotFoundException>(() => storage.OpenReadAsync(key, CancellationToken.None));
    }

    [Fact]
    public async Task Delete_WhenTheServiceIsUnreachable_DoesNotThrow()
    {
        var storage = BuildStorage(serviceUrl: "http://127.0.0.1:1");

        await storage.DeleteAsync("any/key", CancellationToken.None);
    }

    private IFileStorage BuildStorage(string? serviceUrl = null)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:Postgres"] = "Host=unused", // required by AddInfrastructure, never opened here
                ["Email:From"] = "Client Portal <no-reply@client-portal.test>",
                ["Email:SmtpHost"] = "unused",
                ["Storage:ServiceUrl"] = serviceUrl ?? $"http://{rustfs.Hostname}:{rustfs.GetMappedPublicPort(S3Port)}",
                ["Storage:Bucket"] = "client-portal-test",
                ["Storage:AccessKey"] = AccessKey,
                ["Storage:SecretKey"] = SecretKey,
                ["Storage:CreateBucketIfMissing"] = "true",
            })
            .Build();

        var services = new ServiceCollection().AddLogging().AddInfrastructure(configuration);
        return services.BuildServiceProvider().GetRequiredService<IFileStorage>();
    }
}
