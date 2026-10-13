using ClientPortal.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.Infrastructure;

/// <summary>Entry point for filling a development database with test data. The Api calls it; it never decides whether it is allowed to.</summary>
public static class DevelopmentSeed
{
    /// <summary>Owner of the seeded workspaces "Studio Pop" and "Side Project".</summary>
    public const string OwnerEmail = "demo@example.com";

    /// <summary>A plain member of "Studio Pop" and owner of "Other Studio", which the demo owner cannot see.</summary>
    public const string MemberEmail = "member@example.com";

    /// <summary>Shared by both seeded accounts. Local development only.</summary>
    public const string Password = "demo-password";

    /// <summary>Applies pending migrations, then adds the test data unless it is already there.</summary>
    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(cancellationToken);
    }
}
