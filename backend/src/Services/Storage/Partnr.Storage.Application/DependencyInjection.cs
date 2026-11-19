using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Partnr.Storage.Application.Interfaces.Services;
using Partnr.Storage.Application.Services;

namespace Partnr.Storage.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<IFileStorageService, FileStorageService>();

        return services;
    }
}
