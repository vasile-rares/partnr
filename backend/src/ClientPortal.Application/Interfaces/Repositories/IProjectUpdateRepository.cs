using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace and a project: an update of another one is simply not found, even by id.</summary>
public interface IProjectUpdateRepository
{
    Task<ProjectUpdate?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ProjectUpdate>> ListAsync(Guid workspaceId, Guid projectId, bool includeArchived, CancellationToken cancellationToken);

    void Add(ProjectUpdate update);

    void Remove(ProjectUpdate update);
}
