using Partnr.Application.Authorization;
using Partnr.Application.Interfaces.Services;
using Partnr.Application.Mappings;
using Partnr.Application.Messaging;
using Partnr.Application.Notifications;
using Partnr.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Partnr.Application;

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
        services.AddScoped<IPortalMessageService, PortalMessageService>();
        services.AddScoped<IGoogleConnectionService, GoogleConnectionService>();
        services.AddScoped<MeetingResponseBuilder>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IPortalMeetingService, PortalMeetingService>();
        services.AddScoped<IMeetingSyncService, MeetingSyncService>();
        services.AddScoped<IPortalService, PortalService>();
        services.AddScoped<IPortalApprovalService, PortalApprovalService>();
        services.AddScoped<IAuthService, AuthService>();
        services.AddScoped<IPasswordResetService, PasswordResetService>();
        services.AddScoped<WorkspaceAccess>();
        services.AddScoped<IWorkspaceService, WorkspaceService>();

        return services;
    }
}
