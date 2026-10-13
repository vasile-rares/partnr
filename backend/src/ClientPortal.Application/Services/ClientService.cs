using ClientPortal.Application.Authorization;
using ClientPortal.Application.Common;
using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Services;

internal sealed class ClientService(
    IClientRepository clients,
    IClientInvitationRepository invitations,
    ClientResponseBuilder responses,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IClientService
{
    public async Task<ClientResponse> RegisterAsync(
        Guid workspaceId,
        RegisterClientRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var email = Client.NormalizeEmail(request.Email);
        await EnsureEmailIsFreeAsync(workspaceId, email, exceptClientId: null, cancellationToken);

        var client = Client.Register(workspaceId, request.Name, email, request.Company, request.Phone, request.Notes);
        clients.Add(client);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(client, cancellationToken);
    }

    public async Task<IReadOnlyList<ClientResponse>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var list = await clients.ListAsync(workspaceId, includeArchived, cancellationToken);

        return await responses.BuildAsync(list, cancellationToken);
    }

    public async Task<ClientResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        return await responses.BuildAsync(client, cancellationToken);
    }

    public async Task<ClientResponse> UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateClientRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        var current = await responses.BuildAsync(client, cancellationToken);
        if (current.DetailsLocked)
        {
            // While an invitation is pending, or once the client has an account, the details are not ours to change.
            // The form still sends them back unchanged, so only a real difference is refused; the notes always go through.
            if (ChangesIdentity(current, request))
            {
                throw new ConflictException(client.IsLinked
                    ? "This client has an account, so their name, email, phone and company are theirs to change. Only the notes can be edited."
                    : "This client has a pending invitation, so their name, email, phone and company are locked. Cancel the invitation to change them, or edit the notes only.");
            }

            client.UpdateNotes(request.Notes);
        }
        else
        {
            var email = Client.NormalizeEmail(request.Email);
            await EnsureEmailIsFreeAsync(workspaceId, email, exceptClientId: id, cancellationToken);

            client.UpdateDetails(request.Name, email, request.Company, request.Phone, request.Notes);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(client, cancellationToken);
    }

    public async Task<ClientResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        client.Archive();

        // An archived client cannot be invited, so a link still in someone's inbox must stop working.
        var open = await invitations.GetOpenForClientAsync(client.Id, cancellationToken);
        open?.Cancel(time.GetUtcNow().UtcDateTime);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(client, cancellationToken);
    }

    public async Task<ClientResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireOwnerAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        client.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(client, cancellationToken);
    }

    private async Task<Client> RequireClientAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        await clients.GetByIdAsync(workspaceId, id, cancellationToken)
            ?? throw new NotFoundException($"The client with id '{id}' was not found.");

    private async Task EnsureEmailIsFreeAsync(
        Guid workspaceId,
        string email,
        Guid? exceptClientId,
        CancellationToken cancellationToken)
    {
        if (await clients.EmailExistsAsync(workspaceId, email, exceptClientId, cancellationToken))
        {
            throw new ConflictException("A client with this email already exists in this workspace.");
        }
    }

    private static bool ChangesIdentity(ClientResponse current, UpdateClientRequest request) =>
        request.Name.Trim() != current.Name
        || Client.NormalizeEmail(request.Email) != current.Email
        || TextHelpers.Optional(request.Phone) != current.Phone
        || TextHelpers.Optional(request.Company) != current.Company;
}
