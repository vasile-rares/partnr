using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a client of another workspace is simply not found, even by id.</summary>
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    Task<bool> EmailExistsAsync(Guid workspaceId, string email, CancellationToken cancellationToken);

    /// <summary>Tracks the client; it is persisted by <see cref="Common.IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Client client);
}
