using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Partnr.Contracts.Integrations;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Infrastructure.Authentication;
using Partnr.Workspaces.Infrastructure.Email;
using Partnr.Workspaces.Infrastructure.Events;
using Partnr.Workspaces.Infrastructure.Integrations;
using Partnr.Workspaces.Infrastructure.Notifications;
using Partnr.Workspaces.Infrastructure.Persistence;
using Partnr.Workspaces.Infrastructure.Persistence.Repositories;
using Partnr.Workspaces.Infrastructure.Persistence.Seeding;
using Partnr.Workspaces.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace Partnr.Workspaces.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.TryAddSingleton<IInboxPublisher, NoopInboxPublisher>();
        services.TryAddSingleton<IConversationPublisher, NoopConversationPublisher>();
        services.AddSingleton<InboxPushInterceptor>();
        services.AddEvents(configuration, connectionString, (options, sp) => options
            .UseNpgsql(connectionString)
            .AddInterceptors(sp.GetRequiredService<InboxPushInterceptor>()));
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientInvitationRepository, ClientInvitationRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IDeliverableRepository, DeliverableRepository>();
        services.AddScoped<IProjectUpdateRepository, ProjectUpdateRepository>();
        services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
        services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
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

        services.AddOptions<EmailOptions>()
            .Bind(configuration.GetSection(EmailOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IEmailSender, SmtpEmailSender>();
        services.AddSingleton<BackgroundEmailQueue>();
        services.AddSingleton<IBackgroundEmailQueue>(sp => sp.GetRequiredService<BackgroundEmailQueue>());
        services.AddHostedService(sp => sp.GetRequiredService<BackgroundEmailQueue>());

        services.AddOptions<NotificationOptions>()
            .Bind(configuration.GetSection(NotificationOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddHostedService<NotificationEmailWorker>();

        // Google belongs to the Integrations service: this only asks it, over gRPC, whether an account is connected.
        var integrationsAddress = configuration["Services:Integrations:GrpcAddress"]
            ?? throw new InvalidOperationException("Services:Integrations:GrpcAddress is not configured.");
        services.AddGrpcClient<GoogleConnections.GoogleConnectionsClient>(options => options.Address = new Uri(integrationsAddress));
        services.AddScoped<IGoogleConnectionStatus, GrpcGoogleConnectionStatus>();

        services.AddOptions<StorageOptions>()
            .Bind(configuration.GetSection(StorageOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IAmazonS3>(sp =>
        {
            var storage = sp.GetRequiredService<IOptions<StorageOptions>>().Value;
            var config = new AmazonS3Config
            {
                ForcePathStyle = storage.ForcePathStyle,
                // Newer SDKs add checksums that some S3-compatible services (R2, RustFS) do not accept on every call.
                RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
                ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED,
            };

            if (string.IsNullOrWhiteSpace(storage.ServiceUrl))
            {
                config.RegionEndpoint = RegionEndpoint.GetBySystemName(storage.Region);
            }
            else
            {
                config.ServiceURL = storage.ServiceUrl;
                config.AuthenticationRegion = storage.Region;
            }

            return new AmazonS3Client(new BasicAWSCredentials(storage.AccessKey, storage.SecretKey), config);
        });
        services.AddSingleton<IFileStorage, S3FileStorage>();

        return services;
    }
}
