using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Testcontainers.RabbitMq;
using Wolverine;

namespace Partnr.Workspaces.IntegrationTests.Events;

/// <summary>The API with a real RabbitMQ next to its Postgres (needs Docker), and a consumer of its own that keeps the events it receives.</summary>
public sealed class BrokerApiFactory : ApiFactory
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
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Received);
            services.ConfigureWolverine(wolverine => wolverine.Discovery.IncludeType(typeof(CapturingHandler)));
        });
    }
}

public sealed class ReceivedEvents
{
    private readonly List<MeetingChanged> _meetingsChanged = [];

    public IReadOnlyList<MeetingChanged> MeetingsChanged
    {
        get
        {
            lock (_meetingsChanged)
            {
                return [.. _meetingsChanged];
            }
        }
    }

    internal void Add(MeetingChanged received)
    {
        lock (_meetingsChanged)
        {
            _meetingsChanged.Add(received);
        }
    }
}

public static class CapturingHandler
{
    public static void Handle(MeetingChanged received, ReceivedEvents sink) => sink.Add(received);
}
