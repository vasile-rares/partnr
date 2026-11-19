using JasperFx.CodeGeneration.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;
using Wolverine.RabbitMQ;

namespace Partnr.Notifications.Infrastructure.Events;

internal static class EventsServiceCollectionExtensions
{
    /// <summary>
    /// The events of the other services arrive through RabbitMQ: one queue per kind of event for this service
    /// (<c>{Prefix}{ServiceName}.{Event}</c>). This service says nothing back, so it needs no outbox. A message that keeps failing ends in
    /// the dead-letter queue instead of blocking the others.
    /// </summary>
    public static IServiceCollection AddEvents(this IServiceCollection services, IConfiguration configuration)
    {
        var options = configuration.GetSection(EventsOptions.SectionName).Get<EventsOptions>() ?? new EventsOptions();

        services.AddWolverine(wolverine =>
        {
            wolverine.ServiceName = options.ServiceName;
            // The use-case services are internal on purpose (see the architecture tests): generated handler code cannot build them, so it asks the container.
            wolverine.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;

            if (!string.IsNullOrWhiteSpace(options.RabbitMqUri))
            {
                wolverine.UseRabbitMq(new Uri(options.RabbitMqUri))
                    .AutoProvision()
                    .UseConventionalRouting(routing =>
                    {
                        routing.ExchangeNameForSending(type => $"{options.Prefix}{type.Name}");
                        routing.QueueNameForListener(type => $"{options.Prefix}{options.ServiceName}.{type.Name}");
                    });
            }
        });

        return services;
    }
}
