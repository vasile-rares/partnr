using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Application.Services;

namespace Partnr.Integrations.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IGoogleConnectionService, GoogleConnectionService>();
        services.AddScoped<IMeetingSyncService, MeetingSyncService>();
        services.AddScoped<IMeetingQueueService, MeetingQueueService>();

        return services;
    }
}
