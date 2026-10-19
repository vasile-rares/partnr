using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace and a project: a file of another one is simply not found, even by id.</summary>
public interface IProjectFileRepository
{
    Task<ProjectFile?> GetByIdAsync(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken);

    /// <summary>Newest first.</summary>
    Task<IReadOnlyList<ProjectFile>> ListAsync(Guid workspaceId, Guid projectId, bool includeArchived, CancellationToken cancellationToken);

    /// <summary>
    /// What a client may see of one project: shared and not archived, newest first. The caller must already have found
    /// the project through <see cref="IProjectRepository.GetSharedWithClientAsync"/>.
    /// </summary>
    Task<IReadOnlyList<ProjectFile>> ListSharedAsync(Guid projectId, CancellationToken cancellationToken);

    void Add(ProjectFile file);

    void Remove(ProjectFile file);
}
