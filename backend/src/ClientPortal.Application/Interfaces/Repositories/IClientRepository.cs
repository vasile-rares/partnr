using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a client of another workspace is simply not found, even by id.</summary>
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>The workspace's clients ordered by name; archived ones only when asked for.</summary>
    Task<IReadOnlyList<Client>> ListAsync(Guid workspaceId, bool includeArchived, CancellationToken cancellationToken);

    /// <summary>Whether another client of the workspace (archived ones included) already uses the email.</summary>
    /// <param name="exceptClientId">The client being edited, which may keep its own email.</param>
    Task<bool> EmailExistsAsync(Guid workspaceId, string email, Guid? exceptClientId, CancellationToken cancellationToken);

    /// <summary>Whether this account is already linked to a client of the workspace.</summary>
    Task<bool> IsLinkedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracks the client; it is persisted by <see cref="Common.IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Client client);
}
