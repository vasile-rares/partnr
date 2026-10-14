using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Interfaces.Repositories;

public interface IWorkspaceRepository
{
    /// <summary>The workspace with its members, or null.</summary>
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The workspaces the user is a member of (with their members), oldest first.</summary>
    Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>
    /// The workspaces where this account is a client (its card is linked to it and not archived), without members, oldest first.
    /// Being a client is not being a member, so these never show up in <see cref="ListForUserAsync"/>.
    /// </summary>
    Task<IReadOnlyList<Workspace>> ListForClientAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracks the workspace; it is persisted by <see cref="Common.IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Workspace workspace);
}
