using ClientPortal.Application.Common;
using ClientPortal.Application.Workspaces;
using ClientPortal.Domain.Workspaces;

namespace ClientPortal.Tests.Application;

// Hand-written fakes shared by the service tests (services are tested without a database, see backend/CLAUDE.md).

internal sealed class FakeUnitOfWork : IUnitOfWork
{
    public int SaveCount { get; private set; }

    public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default)
    {
        SaveCount++;
        return Task.FromResult(1);
    }
}

internal sealed class FakeCurrentUser(Guid? userId = null) : ICurrentUser
{
    public Guid? UserId { get; set; } = userId;
}

internal sealed class FakeWorkspaceRepository : IWorkspaceRepository
{
    public List<Workspace> Items { get; } = [];

    public Task<Workspace?> GetByIdAsync(Guid id, CancellationToken cancellationToken) =>
        Task.FromResult(Items.FirstOrDefault(w => w.Id == id));

    public Task<IReadOnlyList<Workspace>> ListForUserAsync(Guid userId, CancellationToken cancellationToken) =>
        Task.FromResult<IReadOnlyList<Workspace>>(Items.Where(w => w.RoleOf(userId) is not null).ToList());

    public void Add(Workspace workspace) => Items.Add(workspace);
}
