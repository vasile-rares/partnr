using JasperFx.CodeGeneration.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Identity.Application.Interfaces.Repositories;
using Partnr.Identity.Application.Interfaces.Services;
using Partnr.Identity.Infrastructure.Persistence;
using Wolverine;
using Wolverine.EntityFrameworkCore;
using Wolverine.Postgresql;
using Wolverine.RabbitMQ;

namespace Partnr.Identity.Infrastructure.Events;

internal static class EventsServiceCollectionExtensions
{
    /// <summary>
    /// Events between services: published through Wolverine's outbox in the same transaction as the change (its tables live in the
    /// <c>wolverine</c> schema of this service's own database), then sent to RabbitMQ. Also registers the database context, which has to
    /// be set up the Wolverine way for the outbox to join its transaction.
    /// </summary>
    public static IServiceCollection AddEvents(
        this IServiceCollection services,
        IConfiguration configuration,
        string connectionString,
        Action<DbContextOptionsBuilder, IServiceProvider> configureDbContext)
    {
        var options = configuration.GetSection(EventsOptions.SectionName).Get<EventsOptions>() ?? new EventsOptions();

        services.AddWolverine(wolverine =>
        {
            wolverine.ServiceName = options.ServiceName;
            // The use-case services are internal on purpose (see the architecture tests): generated handler code cannot build them, so it asks the container.
            wolverine.ServiceLocationPolicy = ServiceLocationPolicy.AlwaysAllowed;
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

        services.AddDbContextWithWolverineIntegration<ApplicationDbContext>(
            (serviceProvider, builder) => configureDbContext(builder, serviceProvider));
        services.AddScoped<IUnitOfWork, OutboxUnitOfWork>();
        services.AddScoped<IEventPublisher, OutboxEventPublisher>();

        return services;
    }
}
