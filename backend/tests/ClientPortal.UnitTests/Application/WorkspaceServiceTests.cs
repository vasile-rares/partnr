using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class WorkspaceServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly WorkspaceService _service;

    public WorkspaceServiceTests()
    {
        _currentUser = new FakeCurrentUser(_me);
        _service = new WorkspaceService(_workspaces, new WorkspaceAccess(_workspaces, _currentUser), _unitOfWork);
    }

    [Fact]
    public async Task Create_MakesTheCurrentUserTheOwnerAndSavesOnce()
    {
        var response = await _service.CreateAsync(new CreateWorkspaceRequest { Name = " Studio Pop " }, CancellationToken.None);

        Assert.Equal("Studio Pop", response.Name);
        Assert.Equal("Owner", response.Role);
        var workspace = Assert.Single(_workspaces.Items);
        Assert.Equal(WorkspaceRole.Owner, workspace.RoleOf(_me));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WhenSignedOut_ThrowsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() =>
            _service.CreateAsync(new CreateWorkspaceRequest { Name = "Studio" }, CancellationToken.None));

        Assert.Empty(_workspaces.Items);
    }

    [Fact]
    public async Task ListMine_ReturnsOnlyMyWorkspacesWithMyRole()
    {
        var owned = Workspace.Create("Mine", _me);
        var joined = Workspace.Create("Joined", Guid.NewGuid());
        joined.AddMember(_me, WorkspaceRole.Member);
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.AddRange([owned, joined, someoneElses]);

        var mine = await _service.ListMineAsync(CancellationToken.None);

        Assert.Equal(["Mine", "Joined"], mine.Select(w => w.Name));
        Assert.Equal(["Owner", "Member"], mine.Select(w => w.Role));
    }

    [Fact]
    public async Task Get_AsMember_ReturnsTheWorkspace()
    {
        var workspace = Workspace.Create("Studio", Guid.NewGuid());
        workspace.AddMember(_me, WorkspaceRole.Member);
        _workspaces.Items.Add(workspace);

        var response = await _service.GetAsync(workspace.Id, CancellationToken.None);

        Assert.Equal(workspace.Id, response.Id);
        Assert.Equal("Member", response.Role);
    }

    [Fact]
    public async Task Get_AsNonMember_ThrowsNotFound_LikeAMissingWorkspace()
    {
        var someoneElses = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(someoneElses);

        var notMine = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(someoneElses.Id, CancellationToken.None));
        var missing = await Assert.ThrowsAsync<NotFoundException>(() => _service.GetAsync(Guid.NewGuid(), CancellationToken.None));

        // The two answers must look alike, otherwise ids could be probed.
        Assert.StartsWith("The workspace with id", notMine.Message);
        Assert.StartsWith("The workspace with id", missing.Message);
    }

    [Fact]
    public async Task Rename_AsOwner_ChangesTheNameAndSavesOnce()
    {
        var workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(workspace);

        var response = await _service.RenameAsync(workspace.Id, new RenameWorkspaceRequest { Name = "Studio Pop" }, CancellationToken.None);

        Assert.Equal("Studio Pop", response.Name);
        Assert.Equal("Studio Pop", workspace.Name);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rename_AsMember_ThrowsForbidden_AndChangesNothing()
    {
        var workspace = Workspace.Create("Studio", Guid.NewGuid());
        workspace.AddMember(_me, WorkspaceRole.Member);
        _workspaces.Items.Add(workspace);

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RenameAsync(workspace.Id, new RenameWorkspaceRequest { Name = "Hijacked" }, CancellationToken.None));

        Assert.Equal("Studio", workspace.Name);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Rename_AsNonMember_ThrowsNotFound()
    {
        var workspace = Workspace.Create("Studio", Guid.NewGuid());
        _workspaces.Items.Add(workspace);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RenameAsync(workspace.Id, new RenameWorkspaceRequest { Name = "Hijacked" }, CancellationToken.None));

        Assert.Equal("Studio", workspace.Name);
    }
}
