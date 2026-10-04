using ClientPortal.Application.Clients.Dtos.Requests;
using ClientPortal.Application.Clients.Dtos.Responses;

namespace ClientPortal.Application.Clients;

public interface IClientService
{
    /// <exception cref="Common.Exceptions.ConflictException">The email is already used.</exception>
    Task<ClientResponse> RegisterAsync(RegisterClientRequest request, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The client does not exist.</exception>
    Task<ClientResponse> GetByIdAsync(Guid id, CancellationToken cancellationToken);
}
