using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Infrastructure.Authentication;
using ClientPortal.Infrastructure.Email;
using ClientPortal.Infrastructure.Persistence;
using ClientPortal.Infrastructure.Persistence.Repositories;
using ClientPortal.Infrastructure.Persistence.Seeding;
using ClientPortal.Infrastructure.Storage;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace ClientPortal.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddDbContext<ApplicationDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IClientRepository, ClientRepository>();
        services.AddScoped<IClientInvitationRepository, ClientInvitationRepository>();
        services.AddScoped<IWorkspaceRepository, WorkspaceRepository>();
        services.AddScoped<IProjectRepository, ProjectRepository>();
        services.AddScoped<IDeliverableRepository, DeliverableRepository>();
        services.AddScoped<IProjectUpdateRepository, ProjectUpdateRepository>();
        services.AddScoped<IProjectFileRepository, ProjectFileRepository>();
        services.AddScoped<IApprovalRequestRepository, ApprovalRequestRepository>();
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
