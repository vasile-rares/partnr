using ClientPortal.Application.DTOs.Clients;

namespace ClientPortal.Application.Interfaces.Services;

public interface IClientService
{
    Task<ClientResponse> RegisterAsync(Guid workspaceId, RegisterClientRequest request, CancellationToken cancellationToken);

    Task<IReadOnlyList<ClientResponse>> ListAsync(Guid workspaceId, bool includeArchived, CancellationToken cancellationToken);

    Task<ClientResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    Task<ClientResponse> UpdateAsync(Guid workspaceId, Guid id, UpdateClientRequest request, CancellationToken cancellationToken);

    Task<ClientResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    Task<ClientResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);
}
