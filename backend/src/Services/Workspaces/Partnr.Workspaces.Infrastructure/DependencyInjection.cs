using Partnr.Contracts.Integrations;
using Partnr.Contracts.Storage;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Infrastructure.Authentication;
using Partnr.Workspaces.Infrastructure.Events;
using Partnr.Workspaces.Infrastructure.Integrations;
using Partnr.Workspaces.Infrastructure.Persistence;
using Partnr.Workspaces.Infrastructure.Persistence.Repositories;
using Partnr.Workspaces.Infrastructure.Persistence.Seeding;
using Partnr.Workspaces.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Partnr.Workspaces.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddEvents(configuration, connectionString, (options, _) => options.UseNpgsql(connectionString));
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientInvitationRepository, ClientInvitationRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IDeliverableRepository, DeliverableRepository>();
        services.AddScoped<IProjectUpdateRepository, ProjectUpdateRepository>();
        services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
        services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<IMessageRepository, MessageRepository>();
        services.AddScoped<IConversationReadRepository, ConversationReadRepository>();
        services.AddScoped<IMeetingRepository, MeetingRepository>();
        services.AddScoped<DevelopmentDataSeeder>();
        services.AddIdentityAuthentication();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IAppLinks, AppLinks>();

        // Google belongs to the Integrations service: this only asks it, over gRPC, whether an account is connected.
        var integrationsAddress = configuration["Services:Integrations:GrpcAddress"]
            ?? throw new InvalidOperationException("Services:Integrations:GrpcAddress is not configured.");
        services.AddGrpcClient<GoogleConnections.GoogleConnectionsClient>(options => options.Address = new Uri(integrationsAddress));
        services.AddScoped<IGoogleConnectionStatus, GrpcGoogleConnectionStatus>();

        // The bytes of the files belong to the Storage service: this only talks to it, over gRPC.
        var storageAddress = configuration["Services:Storage:GrpcAddress"]
            ?? throw new InvalidOperationException("Services:Storage:GrpcAddress is not configured.");
        services.AddGrpcClient<Files.FilesClient>(options => options.Address = new Uri(storageAddress));
        services.AddScoped<IFileStorage, GrpcFileStorage>();

        return services;
    }
}
