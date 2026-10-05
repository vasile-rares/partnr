using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Services;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<WorkspaceAccess>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
