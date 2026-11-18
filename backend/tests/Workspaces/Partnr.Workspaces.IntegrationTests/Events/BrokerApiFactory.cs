using System.Collections.Concurrent;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Partnr.Contracts.Events;
using Testcontainers.RabbitMq;
using Wolverine;

namespace Partnr.Workspaces.IntegrationTests.Events;

/// <summary>The API with a real RabbitMQ next to its Postgres (needs Docker), and a consumer of its own that keeps what it receives.</summary>
public sealed class BrokerApiFactory : ApiFactory
{
    private readonly RabbitMqContainer rabbit = new RabbitMqBuilder("rabbitmq:4.4.1-management").Build();

    public ReceivedEvents Received { get; } = new();

    /// <summary>Everything the API logs, so a test can see a handler of the API itself (the probe) do its work.</summary>
    public ConcurrentQueue<string> LogLines { get; } = new();

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
        builder.ConfigureLogging(logging => logging.AddProvider(new CapturingLoggerProvider(LogLines)));
        builder.ConfigureServices(services =>
        {
            services.AddSingleton(Received);
            services.ConfigureWolverine(wolverine => wolverine.Discovery.IncludeType(typeof(CapturingHandler)));
        });
    }
}

public sealed class ReceivedEvents
{
    private readonly List<ProjectCreated> _projectsCreated = [];

    public IReadOnlyList<ProjectCreated> ProjectsCreated
    {
        get
        {
            lock (_projectsCreated)
            {
                return [.. _projectsCreated];
            }
        }
    }

    internal void Add(ProjectCreated received)
    {
        lock (_projectsCreated)
        {
            _projectsCreated.Add(received);
        }
    }
}

public static class CapturingHandler
{
    public static void Handle(ProjectCreated received, ReceivedEvents sink) => sink.Add(received);
}

internal sealed class CapturingLoggerProvider(ConcurrentQueue<string> lines) : ILoggerProvider
{
    public ILogger CreateLogger(string categoryName) => new CapturingLogger(lines);

    public void Dispose()
    {
    }

    private sealed class CapturingLogger(ConcurrentQueue<string> lines) : ILogger
    {
        public IDisposable? BeginScope<TState>(TState state)
            where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception, Func<TState, Exception?, string> formatter) =>
            lines.Enqueue(formatter(state, exception));
    }
}
