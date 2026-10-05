using ClientPortal.Domain.Workspaces;

namespace ClientPortal.Application.Workspaces;

public interface IWorkspaceRepository
{
    /// <summary>The workspace with its members, or null.</summary>
    Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The workspaces the user is a member of (with their members), oldest first.</summary>
    Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken);

    /// <summary>Tracks the workspace; it is persisted by <see cref="Common.IUnitOfWork.SaveChangesAsync"/>.</summary>
    void Add(Workspace workspace);
}
