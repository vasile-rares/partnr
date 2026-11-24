namespace Partnr.Identity.Infrastructure.Events;

internal sealed class EventsOptions
{
    public const string SectionName = "Events";

    /// <summary>The broker, e.g. <c>amqp://partnr:partnr@localhost:5672</c>. Empty: nothing leaves the process (integration tests).</summary>
    public string? RabbitMqUri { get; init; }

    /// <summary>Names this service's queues, so every service gets its own copy of an event.</summary>
    public string ServiceName { get; init; } = "identity";

    /// <summary>Put in front of every exchange and queue, so a test run never shares them with the development one.</summary>
    public string Prefix { get; init; } = "";
}
