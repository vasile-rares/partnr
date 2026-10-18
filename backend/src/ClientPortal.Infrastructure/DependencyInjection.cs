using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Infrastructure.Authentication;
using ClientPortal.Infrastructure.Email;
using ClientPortal.Infrastructure.Persistence;
using ClientPortal.Infrastructure.Persistence.Repositories;
using ClientPortal.Infrastructure.Persistence.Seeding;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;

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

        return services;
    }
}
