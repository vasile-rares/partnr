using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Workspaces where the account is a client (card linked, not archived), without members.</summary>
    Task<IReadOnlyList<Workspace>> ListForClientAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Workspace workspace);
}
