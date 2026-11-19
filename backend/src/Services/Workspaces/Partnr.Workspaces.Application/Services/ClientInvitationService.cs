using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.DTOs.Clients;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Application.Mappings;
using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Services;

internal sealed class ClientInvitationService(
    IClientRepository clients,
    IClientInvitationRepository invitations,
    INotificationPublisher notifications,
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
        await notifications.ClientInvitedAsync(membership.Workspace.Name, client, invitation, token, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

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
}
