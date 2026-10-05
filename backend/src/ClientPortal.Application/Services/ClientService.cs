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
        if (await clients.EmailExistsAsync(workspaceId, email, cancellationToken))
        {
            throw new ConflictException("A client with this email already exists in this workspace.");
        }

        var client = Client.Register(workspaceId, request.Name, email);
        clients.Add(client);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return client.ToResponse();
    }

    public async Task<ClientResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);

        var client = await clients.GetByIdAsync(workspaceId, id, cancellationToken)
            ?? throw new NotFoundException($"The client with id '{id}' was not found.");

        return client.ToResponse();
    }
}
