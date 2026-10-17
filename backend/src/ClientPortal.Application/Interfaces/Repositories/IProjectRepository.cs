using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a project of another workspace is simply not found, even by id.</summary>
public interface IProjectRepository
{
    Task<Project?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>Soonest due first, no due date last.</summary>
    Task<IReadOnlyList<Project>> ListAsync(Guid workspaceId, bool includeArchived, Guid? clientId, CancellationToken cancellationToken);

    /// <summary>Projects shared with this account as a client, soonest due first. Not scoped to a workspace.</summary>
    Task<IReadOnlyList<Project>> ListSharedWithClientAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Null when the project is not shared with this account.</summary>
    Task<Project?> GetSharedWithClientAsync(Guid userId, Guid id, CancellationToken cancellationToken);

    void Add(Project project);
}
