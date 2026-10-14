using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

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
