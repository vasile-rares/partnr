using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a project of another workspace is simply not found, even by id.</summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>The workspace's projects, soonest due first (no due date last); archived ones only when asked for.</summary>
    /// <param name="clientId">Only the projects of this client, when set.</param>
    Task<IReadOnlyList<Project>> ListAsync(Guid workspaceId, bool includeArchived, Guid? clientId, CancellationToken cancellationToken);

    /// <summary>
    /// The projects this account can see as a client, with their stages, soonest due first (no due date last). A project is
    /// shared when its client card is linked to the account and not archived, and the project is not archived and has left
    /// Planning (a draft). Not scoped to a workspace: the client sees every workspace they work with.
    /// </summary>
    Task<IReadOnlyList<Project>> ListSharedWithClientAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>One project from <see cref="ListSharedWithClientAsync"/>, or null when it is not shared with this account.</summary>
    Task<Project?> GetSharedWithClientAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    /// <summary>Tracks the project; it is persisted by <see cref="IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Project project);
}
