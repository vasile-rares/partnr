using ClientPortal.Application.Authorization;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;

namespace ClientPortal.UnitTests.Application;

public class PortalServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly PortalService _service;

    public PortalServiceTests() =>
        _service = new PortalService(_workspaces, new WorkspaceAccess(_workspaces, _currentUser));

    [Fact]
    public async Task ListWorkspaces_ShowsOnlyTheNamesOfTheWorkspacesIAmAClientOf()
    {
        var mine = Workspace.Create("Studio Pop", Guid.NewGuid());
        var other = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.AddRange([mine, other]);
        _workspaces.ClientOf[_me] = [mine.Id];
        _currentUser.UserId = _me;

        var list = await _service.ListWorkspacesAsync(CancellationToken.None);

        var shown = Assert.Single(list);
        Assert.Equal(mine.Id, shown.Id);
        Assert.Equal("Studio Pop", shown.Name);
    }

    [Fact]
    public async Task ListWorkspaces_Anonymous_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.ListWorkspacesAsync(CancellationToken.None));
}
