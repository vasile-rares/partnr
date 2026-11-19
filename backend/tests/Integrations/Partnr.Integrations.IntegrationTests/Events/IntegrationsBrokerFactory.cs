using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Testcontainers.RabbitMq;
using Wolverine;

namespace Partnr.Integrations.IntegrationTests.Events;

/// <summary>
/// Integrations with a real RabbitMQ next to its Postgres (needs Docker), the fake Google behind it, the sync worker on, and a consumer
/// of its own that keeps what the service says to the others.
/// </summary>
public sealed class IntegrationsBrokerFactory : IntegrationsFactory
{
    private readonly RabbitMqContainer rabbit = new RabbitMqBuilder("rabbitmq:4.4.1-management").Build();

    public ReceivedEvents Received { get; } = new();

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
        builder.UseSetting("Google:Fake", "true");
        builder.UseSetting("Google:SyncPollSeconds", "1");
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Received);
            services.ConfigureWolverine(wolverine => wolverine.Discovery.IncludeType(typeof(CapturingHandler)));
        });
    }
}

public sealed class ReceivedEvents
{
    public ConcurrentQueue<MeetingSynced> MeetingsSynced { get; } = new();

    public ConcurrentQueue<GoogleConnected> Connected { get; } = new();

    public ConcurrentQueue<GoogleDisconnected> Disconnected { get; } = new();
}

public static class CapturingHandler
{
    public static void Handle(MeetingSynced received, ReceivedEvents sink) => sink.MeetingsSynced.Enqueue(received);

    public static void Handle(GoogleConnected received, ReceivedEvents sink) => sink.Connected.Enqueue(received);

    public static void Handle(GoogleDisconnected received, ReceivedEvents sink) => sink.Disconnected.Enqueue(received);
}
