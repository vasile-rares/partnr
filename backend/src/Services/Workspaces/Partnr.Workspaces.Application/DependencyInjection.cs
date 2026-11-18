using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Application.Mappings;
using Partnr.Workspaces.Application.Messaging;
using Partnr.Workspaces.Application.Notifications;
using Partnr.Workspaces.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Partnr.Workspaces.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.TryAddSingleton(TimeProvider.System);
        services.AddScoped<ClientResponseBuilder>();
        services.AddScoped<IClientService, ClientService>();
        services.AddScoped<IClientSuggestionService, ClientSuggestionService>();
        services.AddScoped<IClientInvitationService, ClientInvitationService>();
        services.AddScoped<IInvitationService, InvitationService>();
        services.AddScoped<ProjectResponseBuilder>();
        services.AddScoped<ClientProjectResponseBuilder>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<DeliverableResponseBuilder>();
        services.AddScoped<IDeliverableService, DeliverableService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IProjectUpdateService, ProjectUpdateService>();
        services.AddScoped<IProjectFileService, ProjectFileService>();
        services.AddScoped<INotificationWriter, NotificationWriter>();
        services.AddScoped<INotificationEmailService, NotificationEmailService>();
        services.AddScoped<INotificationService, NotificationService>();
        services.AddScoped<ConversationThreads>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<IGoogleConnectionService, GoogleConnectionService>();
        services.AddScoped<MeetingResponseBuilder>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IMeetingSyncService, MeetingSyncService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<WorkspaceAccess>();
        services.AddScoped<ClientAccess>();
        services.AddScoped<ProjectAccess>();
        services.AddScoped<CallerAccess>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
