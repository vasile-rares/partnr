using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a client of another workspace is simply not found, even by id.</summary>
public interface IClientRepository
{
    Task<Client?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Client>> ListAsync(Guid workspaceId, bool includeArchived, CancellationToken cancellationToken);

    /// <summary>Archived ones included; ids of other workspaces are ignored.</summary>
    Task<IReadOnlyList<Client>> ListByIdsAsync(Guid workspaceId, IReadOnlyCollection<Guid> ids, CancellationToken cancellationToken);

    /// <summary>Whether another client of the workspace (archived included) uses the email.</summary>
    Task<bool> EmailExistsAsync(Guid workspaceId, string email, Guid? exceptClientId, CancellationToken cancellationToken);

    /// <summary>Which of these (normalized) emails already belong to a client of the workspace, archived ones included.</summary>
    Task<IReadOnlySet<string>> ExistingEmailsAsync(Guid workspaceId, IReadOnlyCollection<string> emails, CancellationToken cancellationToken);

    Task<bool> IsLinkedAsync(Guid workspaceId, Guid userId, CancellationToken cancellationToken);

    void Add(Client client);
}
