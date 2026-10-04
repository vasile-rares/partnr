using System.Reflection;
using ClientPortal.Application.Common.Messaging;
using FluentValidation;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.Application;

public static class DependencyInjection
{
    private static readonly Assembly ApplicationAssembly = typeof(DependencyInjection).Assembly;

    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddValidatorsFromAssembly(ApplicationAssembly, ServiceLifetime.Scoped);
        services.AddHandlers(typeof(ICommandHandler<,>));
        services.AddHandlers(typeof(IQueryHandler<,>));

        return services;
    }

    private static void AddHandlers(this IServiceCollection services, Type handlerInterface)
    {
        var handlers = ApplicationAssembly.GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false })
            .SelectMany(t => t.GetInterfaces()
                .Where(i => i.IsGenericType && i.GetGenericTypeDefinition() == handlerInterface)
                .Select(i => (Service: i, Implementation: t)));

        foreach (var (service, implementation) in handlers)
        {
            services.AddScoped(service, implementation);
        }
    }
}
