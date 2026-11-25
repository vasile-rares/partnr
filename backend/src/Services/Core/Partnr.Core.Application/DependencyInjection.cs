using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Application.Mappings;
using Partnr.Core.Application.Messaging;
using Partnr.Core.Application.Notifications;
using Partnr.Core.Application.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Partnr.Core.Application;

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
        services.AddScoped<IMemberService, MemberService>();
        services.AddScoped<IMemberInvitationService, MemberInvitationService>();
        services.AddScoped<ProjectResponseBuilder>();
        services.AddScoped<ClientProjectResponseBuilder>();
        services.AddScoped<IProjectService, ProjectService>();
        services.AddScoped<DeliverableResponseBuilder>();
        services.AddScoped<IDeliverableService, DeliverableService>();
        services.AddScoped<IApprovalService, ApprovalService>();
        services.AddScoped<IProjectUpdateService, ProjectUpdateService>();
        services.AddScoped<IProjectFileService, ProjectFileService>();
        services.AddScoped<INotificationPublisher, NotificationPublisher>();
        services.AddScoped<ConversationThreads>();
        services.AddScoped<IMessageService, MessageService>();
        services.AddScoped<MeetingResponseBuilder>();
        services.AddScoped<IMeetingService, MeetingService>();
        services.AddScoped<IMeetingSyncResultService, MeetingSyncResultService>();
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
