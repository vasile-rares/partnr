using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Partnr.Notifications.Application.Interfaces.Services;
using Partnr.Notifications.Application.Services;

namespace Partnr.Notifications.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<INotificationEmailService, NotificationEmailService>();
        services.AddScoped<INotificationEventService, NotificationEventService>();

        return services;
    }
}
