using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Identity.Application.Interfaces.Repositories;
using Partnr.Identity.Application.Interfaces.Services;
using Partnr.Identity.Infrastructure.Authentication;
using Partnr.Identity.Infrastructure.Events;
using Partnr.Identity.Infrastructure.Persistence;
using Partnr.Identity.Infrastructure.Persistence.Repositories;

namespace Partnr.Identity.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddEvents(configuration, connectionString, (options, _) => options.UseNpgsql(connectionString));
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddIdentityAuthentication();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        return services;
    }

    /// <summary>Creates the database and applies the migrations. For development; elsewhere the migrations run as a deployment step.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<ApplicationDbContext>().Database.MigrateAsync();
    }
}
