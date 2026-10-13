using ClientPortal.Application.Authorization;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace ClientPortal.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ClientResponseBuilder>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IClientInvitationService, ClientInvitationService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<WorkspaceAccess>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
