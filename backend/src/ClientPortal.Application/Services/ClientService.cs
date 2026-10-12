using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Services;

internal sealed class ClientService(IClientRepository clients, WorkspaceAccess access, IUnitOfWork unitOfWork) : IClientService
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

        return client.ToResponse();
    }

    public async Task<IReadOnlyList<ClientResponse>> ListAsync(
        Guid workspaceId,
        bool includeArchived,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var list = await clients.ListAsync(workspaceId, includeArchived, cancellationToken);

        return list.Select(c => c.ToResponse()).ToList();
    }

    public async Task<ClientResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        return client.ToResponse();
    }

    public async Task<ClientResponse> UpdateAsync(
        Guid workspaceId,
        Guid id,
        UpdateClientRequest request,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        var email = Client.NormalizeEmail(request.Email);
        await EnsureEmailIsFreeAsync(workspaceId, email, exceptClientId: id, cancellationToken);

        client.UpdateDetails(request.Name, email, request.Company, request.Phone, request.Notes);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return client.ToResponse();
    }

    public async Task<ClientResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        client.Archive();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return client.ToResponse();
    }

    public async Task<ClientResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var client = await RequireClientAsync(workspaceId, id, cancellationToken);

        client.Restore();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return client.ToResponse();
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
}
