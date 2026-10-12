using ClientPortal.Application.DTOs.Clients;

namespace ClientPortal.Application.Interfaces.Services;

public interface IClientService
{
    /// <exception cref="Common.Exceptions.NotFoundException">The workspace does not exist or the user is not a member.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">The email is already used in this workspace.</exception>
    Task<ClientResponse> RegisterAsync(Guid workspaceId, RegisterClientRequest request, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The workspace does not exist or the user is not a member.</exception>
    Task<IReadOnlyList<ClientResponse>> ListAsync(Guid workspaceId, bool includeArchived, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The workspace or the client does not exist, or the user is not a member.</exception>
    Task<ClientResponse> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The workspace or the client does not exist, or the user is not a member.</exception>
    /// <exception cref="Common.Exceptions.ConflictException">Another client of this workspace already uses the email.</exception>
    Task<ClientResponse> UpdateAsync(Guid workspaceId, Guid id, UpdateClientRequest request, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The workspace or the client does not exist, or the user is not a member.</exception>
    Task<ClientResponse> ArchiveAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <exception cref="Common.Exceptions.NotFoundException">The workspace or the client does not exist, or the user is not a member.</exception>
    Task<ClientResponse> RestoreAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);
}
