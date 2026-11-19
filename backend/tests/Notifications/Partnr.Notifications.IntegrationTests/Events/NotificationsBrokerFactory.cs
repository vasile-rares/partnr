using Microsoft.AspNetCore.Hosting;
using Testcontainers.RabbitMq;

namespace Partnr.Notifications.IntegrationTests.Events;

/// <summary>Notifications with a real RabbitMQ next to its Postgres (needs Docker), and the email worker on, so the whole trip can be seen.</summary>
public sealed class NotificationsBrokerFactory : NotificationsFactory
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
        builder.UseSetting("Notifications:EmailWorkerEnabled", "true");
        builder.UseSetting("Notifications:EmailPollSeconds", "1");
    }
}
