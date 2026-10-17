using ClientPortal.Infrastructure.Persistence.Seeding;
using Microsoft.Extensions.DependencyInjection;

namespace ClientPortal.Infrastructure;

public static class DevelopmentSeed
{
    /// <summary>Owner of the seeded workspaces "Studio Pop" and "Side Project".</summary>
    public const string OwnerEmail = "demo@example.com";

    /// <summary>A plain member of "Studio Pop" and owner of "Other Studio", which the demo owner cannot see.</summary>
    public const string MemberEmail = "member@example.com";

    /// <summary>A client of "Studio Pop" who already accepted an invitation and has an account of their own.</summary>
    public const string ClientEmail = "client@example.com";

    /// <summary>Fixed so the invitation page opens without an email: /invite/{PendingInvitationToken}. Development only.</summary>
    public const string PendingInvitationToken = "dev-pending-invitation-token";

    /// <summary>Shared by all the seeded accounts. Local development only.</summary>
    public const string Password = "demo-password";

    /// <summary>
    /// Like <see cref="SeedDevelopmentDataAsync"/>, but first empties and rebuilds the database when the seed changed.
    /// Everything in it is lost; only for a database on this machine.
    /// </summary>
    public static async Task ReseedDevelopmentDataIfChangedAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().ReseedIfChangedAsync(cancellationToken);
    }

    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(cancellationToken);
    }
}
