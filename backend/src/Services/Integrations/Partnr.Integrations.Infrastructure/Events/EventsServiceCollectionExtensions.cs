using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Partnr.Integrations.Infrastructure.Events;

internal static class EventsServiceCollectionExtensions
{
    /// <summary>
    /// Events between services: published through Wolverine's outbox in the same transaction as the change (its tables live in the
    /// <c>wolverine</c> schema of this service's own database), then sent to RabbitMQ. Also registers the database context, which has to
    /// be set up the Wolverine way for the outbox to join its transaction.
    /// </summary>
    public static IServiceCollection AddEvents(this IServiceCollection services, IConfiguration configuration, string connectionString)
    {
        var options = configuration.GetSection(EventsOptions.SectionName).Get<EventsOptions>() ?? new EventsOptions();

        services.AddWolverine(wolverine =>
        {
            wolverine.ServiceName = options.ServiceName;
            wolverine.PersistMessagesWithPostgresql(connectionString, "wolverine");
            wolverine.UseEntityFrameworkCoreTransactions();
            wolverine.Policies.UseDurableOutboxOnAllSendingEndpoints();

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

        services.AddDbContextWithWolverineIntegration<IntegrationsDbContext>(
            (serviceProvider, builder) => builder.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, OutboxUnitOfWork>();

        return services;
    }
}
