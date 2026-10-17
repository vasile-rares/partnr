using System.Globalization;
using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.DTOs.Email;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Services;

internal sealed class ClientInvitationService(
    IClientRepository clients,
    IClientInvitationRepository invitations,
    IIdentityService identity,
    IEmailSender email,
    IAppLinks links,
    WorkspaceAccess access,
    ClientResponseBuilder responses,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IClientInvitationService
{
    public async Task<ClientResponse> InviteAsync(Guid workspaceId, Guid clientId, CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, clientId, cancellationToken);

        if (client.IsArchived)
        {
            throw new ConflictException("An archived client cannot be invited. Restore the client first.");
        }

        if (client.IsLinked)
        {
            throw new ConflictException("This client already has access to the portal.");
        }

        var now = time.GetUtcNow().UtcDateTime;

        // Cancelled in its own save: the unique index allows one open invitation per client.
        var previous = await invitations.GetOpenForClientAsync(client.Id, cancellationToken);
        if (previous is not null)
        {
            previous.Cancel(now);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var (token, hash) = ClientInvitation.NewToken();
        var invitation = ClientInvitation.Create(workspaceId, client.Id, client.Email, hash, membership.UserId, now);
        invitations.Add(invitation);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        // Saved before sending: if sending fails the freelancer can resend, but a delivered link must have an invitation behind it.
        var inviter = await identity.FindByIdAsync(membership.UserId, cancellationToken);
        await email.SendAsync(
            BuildEmail(client, invitation, membership.Workspace.Name, inviter?.FullName, links.InvitationUrl(token)),
            cancellationToken);

        return await responses.BuildAsync(client, cancellationToken);
    }

    public async Task<ClientResponse> CancelAsync(Guid workspaceId, Guid clientId, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, clientId, cancellationToken);

        if (client.IsLinked)
        {
            throw new ConflictException("This client already accepted the invitation, so there is nothing to cancel.");
        }

        var open = await invitations.GetOpenForClientAsync(client.Id, cancellationToken);
        if (open is not null)
        {
            open.Cancel(time.GetUtcNow().UtcDateTime);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return await responses.BuildAsync(client, cancellationToken);
    }

    private async Task<Client> RequireClientAsync(Guid workspaceId, Guid clientId, CancellationToken cancellationToken) =>
        await clients.GetByIdAsync(workspaceId, clientId, cancellationToken)
            ?? throw new NotFoundException($"The client with id '{clientId}' was not found.");

    private static EmailMessage BuildEmail(Client client, ClientInvitation invitation, string workspaceName, string? inviterName, string link)
    {
        var from = string.IsNullOrWhiteSpace(inviterName) ? workspaceName : $"{inviterName} ({workspaceName})";
        var expires = invitation.ExpiresAtUtc.ToString("d MMMM yyyy", CultureInfo.InvariantCulture);
        var greeting = string.IsNullOrWhiteSpace(client.Name) ? "Hi," : $"Hi {client.Name},";

        return new EmailMessage(
            invitation.Email,
            $"{workspaceName} invited you to follow your projects",
            $"""
            {greeting}

            {from} invited you to Client Portal, where you can follow your projects and see what is happening.

            Open this link to create your account or sign in:
            {link}

            The link works until {expires}, and only with this email address ({invitation.Email}).
            If you were not expecting this message, you can ignore it.
            """);
    }
}
