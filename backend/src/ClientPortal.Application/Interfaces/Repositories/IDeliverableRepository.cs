using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace and a project: a deliverable of another one is simply not found, even by id.</summary>
public interface IDeliverableRepository
{
    Task<Deliverable?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<Deliverable>> ListAsync(Guid workspaceId, Guid projectId, bool includeArchived, CancellationToken cancellationToken);

    void Add(Deliverable deliverable);

    void Remove(Deliverable deliverable);
}
