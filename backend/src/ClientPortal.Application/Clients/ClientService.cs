using ClientPortal.Application.Clients.Dtos.Requests;
using ClientPortal.Application.Clients.Dtos.Responses;
using ClientPortal.Application.Common;
using ClientPortal.Application.Common.Exceptions;
using ClientPortal.Domain.Clients;

namespace ClientPortal.Application.Clients;

internal sealed class ClientService(IClientRepository clients, IUnitOfWork unitOfWork) : IClientService
{
    public async Task<ClientResponse> RegisterAsync(RegisterClientRequest request, CancellationToken cancellationToken)
    {
        var email = Client.NormalizeEmail(request.Email);
        if (await clients.EmailExistsAsync(email, cancellationToken))
        {
            throw new ConflictException("A client with this email already exists.");
        }

        var client = Client.Register(request.Name, email);
        clients.Add(client);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return client.ToResponse();
    }

    public async Task<ClientResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken)
    {
        var client = await clients.GetByIdAsync(id, cancellationToken)
            ?? throw new NotFoundException($"The client with id '{id}' was not found.");

        return client.ToResponse();
    }
}
