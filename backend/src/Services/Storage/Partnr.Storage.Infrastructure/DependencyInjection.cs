using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Partnr.Storage.Application.Interfaces.Repositories;
using Partnr.Storage.Application.Interfaces.Services;
using Partnr.Storage.Infrastructure.Blobs;
using Partnr.Storage.Infrastructure.Events;
using Partnr.Storage.Infrastructure.Persistence;
using Partnr.Storage.Infrastructure.Persistence.Repositories;

namespace Partnr.Storage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration.GetConnectionString("Postgres")
            ?? throw new InvalidOperationException("Connection string 'Postgres' is not configured.");

        services.AddEvents(configuration);
        services.AddDbContext<StorageDbContext>(options => options.UseNpgsql(connectionString));
        services.AddScoped<IUnitOfWork, EfUnitOfWork>();
        services.AddScoped<IStoredFileRepository, StoredFileRepository>();

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
        services.AddSingleton<IBlobStore, S3BlobStore>();

        return services;
    }

    /// <summary>Creates the database and applies the migrations. For development; elsewhere the migrations run as a deployment step.</summary>
    public static async Task MigrateDatabaseAsync(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<StorageDbContext>().Database.MigrateAsync();
    }
}
