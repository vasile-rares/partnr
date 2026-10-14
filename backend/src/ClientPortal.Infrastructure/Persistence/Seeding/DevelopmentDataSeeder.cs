using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace ClientPortal.Infrastructure.Persistence.Seeding;

/// <summary>
/// Fills an empty development database with accounts, workspaces and clients to click through. It goes through the same
/// ports as the services (identity, repositories, unit of work), so the data obeys the same rules as real data.
/// </summary>
internal sealed class DevelopmentDataSeeder(
    ApplicationDbContext dbContext,
    IIdentityService identity,
    IWorkspaceRepository workspaces,
    IClientRepository clients,
    IClientInvitationRepository invitations,
    IProjectRepository projects,
    IAppLinks links,
    IUnitOfWork unitOfWork,
    ILogger<DevelopmentDataSeeder> logger)
{
    private const string FingerprintPrefix = "seed:";

    /// <summary>
    /// Seeds, and empties the database first (every table is dropped and the migrations recreate them) when the test data changed
    /// since it was last seeded. Everything in it is lost, including data entered by hand, so it only ever runs against a
    /// database on this machine. The fingerprint of the
    /// seed that filled the database is kept as the database's own comment, which needs no table and no migration.
    /// </summary>
    public async Task ReseedIfChangedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken); // also creates the database when it does not exist

        var seeded = await identity.FindByEmailAsync(DevelopmentSeed.OwnerEmail, cancellationToken) is not null;
        var stored = await ReadFingerprintAsync(cancellationToken);

        if (seeded && stored == SeedFingerprint.Current)
        {
            logger.LogInformation("Development data is up to date with the seed ({Fingerprint}).", SeedFingerprint.Current);
            return;
        }

        if (seeded)
        {
            EnsureDatabaseIsLocal();
            logger.LogWarning(
                "The seed data changed ({Stored} -> {Current}). Recreating the development tables; anything entered by hand is lost.",
                stored ?? "unknown",
                SeedFingerprint.Current);

            // Dropping the schema instead of the database: dropping needs a connection to another database and fails while any
            // other program (a SQL client, say) is connected, and this works whatever the database is called.
            await dbContext.Database.ExecuteSqlRawAsync("DROP SCHEMA public CASCADE; CREATE SCHEMA public;", cancellationToken);
        }

        await SeedAsync(cancellationToken);
        await WriteFingerprintAsync(cancellationToken);
    }

    private void EnsureDatabaseIsLocal()
    {
        var host = new NpgsqlConnectionStringBuilder(dbContext.Database.GetConnectionString()).Host;
        if (host is not ("localhost" or "127.0.0.1" or "::1"))
        {
            throw new InvalidOperationException($"Refusing to recreate a database on '{host}': automatic reseeding only works on this machine.");
        }
    }

    private async Task<string?> ReadFingerprintAsync(CancellationToken cancellationToken)
    {
        var comment = await dbContext.Database
            .SqlQueryRaw<string>(
                "SELECT COALESCE(shobj_description(oid, 'pg_database'), '') AS \"Value\" FROM pg_database WHERE datname = current_database()")
            .SingleAsync(cancellationToken);

        return comment.StartsWith(FingerprintPrefix, StringComparison.Ordinal) ? comment[FingerprintPrefix.Length..] : null;
    }

    // COMMENT and DO blocks cannot take parameters. The value is 16 hex digits we computed ourselves (SeedFingerprint), never
    // input, so building the statement as text is safe here.
#pragma warning disable EF1002
    private Task WriteFingerprintAsync(CancellationToken cancellationToken) =>
        dbContext.Database.ExecuteSqlRawAsync(
            $"DO $$ BEGIN EXECUTE format('COMMENT ON DATABASE %I IS %L', current_database(), '{FingerprintPrefix}{SeedFingerprint.Current}'); END $$",
            cancellationToken);
#pragma warning restore EF1002

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        await dbContext.Database.MigrateAsync(cancellationToken);

        // The owner account marks the seed as done, so running it again changes nothing.
        if (await identity.FindByEmailAsync(DevelopmentSeed.OwnerEmail, cancellationToken) is not null)
        {
            logger.LogInformation("Development data is already there ({Email}); nothing to do.", DevelopmentSeed.OwnerEmail);
            return;
        }

        var owner = await identity.CreateAsync(DevelopmentSeed.OwnerEmail, "Ana Pop", DevelopmentSeed.Password, cancellationToken);
        // The owner has a complete profile; the member leaves the optional details empty.
        await identity.UpdateProfileAsync(owner.Id, owner.FullName, "+40 722 000 000", "Studio Pop SRL", cancellationToken);
        var member = await identity.CreateAsync(DevelopmentSeed.MemberEmail, "Mihai Member", DevelopmentSeed.Password, cancellationToken);

        // The main workspace: the owner plus a member, with a mix of complete, sparse and archived clients.
        var studio = Workspace.Create("Studio Pop", owner.Id);
        studio.AddMember(member.Id, WorkspaceRole.Member);
        workspaces.Add(studio);
        foreach (var client in StudioClients(studio.Id))
        {
            clients.Add(client);
        }

        // A client who accepted an invitation: the card is linked to a real account and reads its details from it.
        var account = await identity.CreateAsync(DevelopmentSeed.ClientEmail, "Elena Client", DevelopmentSeed.Password, cancellationToken);
        await identity.UpdateProfileAsync(account.Id, account.FullName, "+40 723 000 000", "Elena Design", cancellationToken);
        await identity.ConfirmEmailAsync(account.Id, cancellationToken);
        var linked = Client.Register(studio.Id, "Elena (card)", DevelopmentSeed.ClientEmail, "Old company", "000", "Prefers morning calls.");
        linked.LinkTo(account.Id, DateTime.UtcNow);
        clients.Add(linked);

        // Projects for that client: an active one Elena sees in her portal, and a draft in Planning she does not.
        var website = Project.Create(
            studio.Id,
            linked.Id,
            "Elena Design website",
            "A new portfolio website with a booking page.",
            new DateOnly(2026, 10, 1),
            new DateOnly(2026, 12, 15));
        website.ChangeStatus(ProjectStatus.Active);
        var discovery = website.AddStage("Discovery", "Goals, audience and the pages needed.", new DateOnly(2026, 10, 15));
        var design = website.AddStage("Design", "Layouts for every page, approved by the client.", new DateOnly(2026, 11, 5));
        var development = website.AddStage("Development", null, new DateOnly(2026, 12, 1));
        website.AddStage("Launch", null, new DateOnly(2026, 12, 15));
        website.UpdateStage(discovery.Id, discovery.Name, discovery.Description, discovery.DueDate, StageStatus.Done);
        website.UpdateStage(design.Id, design.Name, design.Description, design.DueDate, StageStatus.Done);
        website.UpdateStage(development.Id, development.Name, development.Description, development.DueDate, StageStatus.InProgress);
        projects.Add(website);
        projects.Add(Project.Create(studio.Id, linked.Id, "Brand refresh", "Logo and colours, after the website."));

        // A client with an invitation waiting, to try the invitation page and see the locked details.
        var pending = Client.Register(studio.Id, "Pat Pending", "pat@pending.example", "Pending Co");
        clients.Add(pending);
        invitations.Add(ClientInvitation.Create(
            studio.Id,
            pending.Id,
            pending.Email,
            ClientInvitation.HashToken(DevelopmentSeed.PendingInvitationToken),
            owner.Id,
            DateTime.UtcNow));

        // A workspace with no clients, to see the empty states and the workspace picker.
        workspaces.Add(Workspace.Create("Side Project", owner.Id));

        // A workspace the owner does not belong to, to check that it stays invisible.
        var other = Workspace.Create("Other Studio", member.Id);
        workspaces.Add(other);
        clients.Add(Client.Register(other.Id, "Hidden Client", "hidden@example.com"));

        await unitOfWork.SaveChangesAsync(cancellationToken);

        logger.LogInformation(
            "Seeded development data. Sign in as {Owner} (owner) or {Member} (member) with password '{Password}'.",
            DevelopmentSeed.OwnerEmail,
            DevelopmentSeed.MemberEmail,
            DevelopmentSeed.Password);
        logger.LogInformation(
            "Also: {Client} is a client of Studio Pop (same password); the pending invitation is {Link}.",
            DevelopmentSeed.ClientEmail,
            links.InvitationUrl(DevelopmentSeed.PendingInvitationToken));
    }

    private static IEnumerable<Client> StudioClients(Guid workspaceId)
    {
        yield return Client.Register(
            workspaceId, "Acme SRL", "contact@acme.example", "Acme Group", "+40 721 000 001", "Main contact: Elena. Pays within 14 days.");
        yield return Client.Register(
            workspaceId, "Bright Bakery", "hello@brightbakery.example", "Bright Bakery", "+40 721 000 002", "Wants a new website before the spring opening.");
        yield return Client.Register(workspaceId, "Cosmin Dobre", "cosmin@dobre.example");
        // Added with just an email, the way the add-client form does: the list shows the address until the client has an account.
        yield return Client.Register(workspaceId, null, "just.an.email@example.com");
        yield return Client.Register(workspaceId, "Delta Dental", "office@deltadental.example", "Delta Dental Clinic", "+40 721 000 004");
        yield return Client.Register(
            workspaceId, "Evergreen Studio", "team@evergreen.example", "Evergreen", notes: "Prefers email over phone calls.");
        yield return Client.Register(workspaceId, "Fabrica de Idei", "info@fabricadeidei.example", "Fabrica de Idei SA", "+40 721 000 006");

        // Archived clients, hidden from the default list.
        var oldClient = Client.Register(workspaceId, "Old Client SRL", "old@oldclient.example", "Old Client", notes: "Project finished in 2025.");
        oldClient.Archive();
        yield return oldClient;

        var paused = Client.Register(workspaceId, "Paused Partners", "paused@partners.example", "Paused Partners", "+40 721 000 008");
        paused.Archive();
        yield return paused;
    }
}
