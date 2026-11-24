using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Workspaces where the account is a client (card linked, not archived). Members are loaded too: a client's message has to reach them.</summary>
    Task<IReadOnlyList<Workspace>> ListForClientAsync(Guid userId, CancellationToken cancellationToken);

    void Add(Workspace workspace);
}
