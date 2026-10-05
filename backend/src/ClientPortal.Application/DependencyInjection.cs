using ClientPortal.Application.Auth;
using ClientPortal.Application.Clients;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IAuthService, AuthService>();

        return services;
    }
}
