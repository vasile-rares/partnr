using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Testcontainers.RabbitMq;
using Wolverine;

namespace Partnr.Storage.IntegrationTests.Events;

// The whole trip of a deletion: RabbitMQ carries the event and the service removes the bytes.
public sealed class ProjectFileRemovedOverTheBrokerTests(ProjectFileRemovedOverTheBrokerTests.BrokerFactory factory) : IClassFixture<ProjectFileRemovedOverTheBrokerTests.BrokerFactory>
{
    [Fact]
    public async Task AFileRemovedElsewhere_LosesItsBytesHere()
    {
        var key = $"{Guid.NewGuid()}/{Guid.NewGuid()}/{Guid.NewGuid()}";
        await factory.Blobs.PutAsync(key, new MemoryStream([1, 2, 3]), "application/zip", CancellationToken.None);

        using (var scope = factory.Services.CreateScope())
        {
            await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new ProjectFileRemoved(key));
        }

        var deadline = DateTime.UtcNow.AddSeconds(30);
        while (factory.Blobs.Contains(key) && DateTime.UtcNow < deadline)
        {
            await Task.Delay(100);
        }

        Assert.False(factory.Blobs.Contains(key));
    }

    /// <summary>Storage with a real RabbitMQ next to its Postgres (needs Docker).</summary>
    public sealed class BrokerFactory : StorageFactory
    {
        private readonly RabbitMqContainer rabbit = new RabbitMqBuilder("rabbitmq:4.4.1-management").Build();

        public override async Task InitializeAsync()
        {
            // Before the base class: it builds the host, which reads the broker's address.
            await rabbit.StartAsync();
            await base.InitializeAsync();
        }

        protected override Task DisposeContainersAsync() => rabbit.DisposeAsync().AsTask();

        protected override void ConfigureWebHost(IWebHostBuilder builder)
        {
            base.ConfigureWebHost(builder);

            builder.UseSetting("Events:RabbitMqUri", rabbit.GetConnectionString());
        }
    }
}
