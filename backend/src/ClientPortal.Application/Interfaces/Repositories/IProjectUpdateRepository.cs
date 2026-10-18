using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace and a project: an update of another one is simply not found, even by id.</summary>
public interface IProjectUpdateRepository
{
    Task<ProjectUpdate?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ProjectUpdate>> ListAsync(Guid workspaceId, Guid projectId, bool includeArchived, CancellationToken cancellationToken);

    /// <summary>
    /// What a client may see of one project: shared and not archived, the most recently shared first. The caller must
    /// already have found the project through <see cref="IProjectRepository.GetSharedWithClientAsync"/>.
    /// </summary>
    Task<IReadOnlyList<ProjectUpdate>> ListSharedAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(ProjectUpdate update);

    void Remove(ProjectUpdate update);
}
