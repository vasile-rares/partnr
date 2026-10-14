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

    /// <summary>A client of "Studio Pop" who already accepted an invitation and has an account of their own.</summary>
    public const string ClientEmail = "client@example.com";

    /// <summary>
    /// The token of a pending invitation (for the card "Pat Pending" in "Studio Pop"), fixed so the invitation page can be
    /// opened without an email: /invite/{PendingInvitationToken}. A real token is random; this one is for development only.
    /// </summary>
    public const string PendingInvitationToken = "dev-pending-invitation-token";

    /// <summary>Shared by all the seeded accounts. Local development only.</summary>
    public const string Password = "demo-password";

    /// <summary>
    /// Like <see cref="SeedDevelopmentDataAsync"/>, but when the seed itself was edited since the database was seeded, the database
    /// is emptied and rebuilt first, so it always matches the current test data. Everything in it is lost. Only for a database on this machine.
    /// </summary>
    public static async Task ReseedDevelopmentDataIfChangedAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().ReseedIfChangedAsync(cancellationToken);
    }

    /// <summary>Applies pending migrations, then adds the test data unless it is already there.</summary>
    public static async Task SeedDevelopmentDataAsync(this IServiceProvider services, CancellationToken cancellationToken = default)
    {
        await using var scope = services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<DevelopmentDataSeeder>().SeedAsync(cancellationToken);
    }
}
