using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a project of another workspace is simply not found, even by id.</summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>The workspace's projects, soonest due first (no due date last); archived ones only when asked for.</summary>
    /// <param name="clientId">Only the projects of this client, when set.</param>
    Task<IReadOnlyList<Project>> ListAsync(Guid workspaceId, bool includeArchived, Guid? clientId, CancellationToken cancellationToken);

    /// <summary>Tracks the project; it is persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Project project);
}
