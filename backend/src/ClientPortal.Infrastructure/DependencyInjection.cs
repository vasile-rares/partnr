using ClientPortal.Application.Common.Interfaces;
using ClientPortal.Domain.Interfaces;
using ClientPortal.Infrastructure.Persistence;
using ClientPortal.Infrastructure.Persistence.Repositories;
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

        return services;
    }
}
