using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Infrastructure.Authentication;
using Partnr.Integrations.Infrastructure.Events;
using Partnr.Integrations.Infrastructure.Google;
using Partnr.Integrations.Infrastructure.Persistence;
using Partnr.Integrations.Infrastructure.Persistence.Repositories;

namespace Partnr.Integrations.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddEvents(configuration, connectionString);
        services.AddScoped<IGoogleConnectionRepository, GoogleConnectionRepository>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddOptions<AppOptions>()
            .Bind(configuration.GetSection(AppOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddScoped<IAppLinks, AppLinks>();

        services.AddOptions<GoogleOptions>()
            .Bind(configuration.GetSection(GoogleOptions.SectionName))
            .ValidateDataAnnotations()
            .ValidateOnStart();
        services.AddSingleton<IValidateOptions<GoogleOptions>, GoogleOptionsValidator>();
        services.AddSingleton<ISecretProtector, SecretProtector>();
        if (configuration.GetSection(GoogleOptions.SectionName).GetValue<bool>(nameof(GoogleOptions.Fake)))
        {
            services.AddSingleton<IGoogleOAuthClient, FakeGoogleOAuthClient>();
        }
        else
        {
            services.AddHttpClient<IGoogleOAuthClient, GoogleOAuthClient>(client => client.Timeout = TimeSpan.FromSeconds(15));
        }

        return services;
    }

    /// <summary>Creates the database and applies the migrations. For development; elsewhere the migrations run as a deployment step.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IntegrationsDbContext>().Database.MigrateAsync();
    }
}
