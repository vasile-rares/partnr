using Partnr.Application.Authorization;
using Partnr.Application.Exceptions;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.UnitTests.Application;

public class CallerAccessTests
{
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly CallerAccess _callers;
    private readonly Guid _owner = Guid.NewGuid();
    private readonly Guid _member = Guid.NewGuid();
    private readonly Guid _client = Guid.NewGuid();
    private readonly Workspace _studio;

    public CallerAccessTests()
    {
        _studio = Workspace.Create("Studio Pop", _owner);
        _studio.AddMember(_member, WorkspaceRole.Member);
        _workspaces.Items.Add(_studio);
        _workspaces.ClientOf[_client] = [_studio.Id];
        var access = new WorkspaceAccess(_workspaces, _currentUser);
        _callers = new CallerAccess(access, new ClientAccess(new FakeProjectRepository(), _workspaces));
    }

    [Theory]
    [InlineData(WorkspaceRole.Owner)]
    [InlineData(WorkspaceRole.Member)]
    public async Task AMember_IsACallerWithTheirRole(WorkspaceRole role)
    {
        var userId = role == WorkspaceRole.Owner ? _owner : _member;
        _currentUser.UserId = userId;

        var caller = await _callers.RequireAsync(_studio.Id, CancellationToken.None);

        Assert.Equal(role, caller.Role);
        Assert.False(caller.IsClient);
        Assert.Equal(userId, caller.UserId);
    }

    [Fact]
    public async Task AClient_IsACallerWithNoRole()
    {
        _currentUser.UserId = _client;

        var caller = await _callers.RequireAsync(_studio.Id, CancellationToken.None);

        Assert.True(caller.IsClient);
        Assert.Null(caller.Role);
        Assert.Same(_studio, caller.Workspace);
    }

    [Fact]
    public async Task Anyone_Else_AndAnUnknownWorkspace_AreNotFound()
    {
        _currentUser.UserId = Guid.NewGuid();
        await Assert.ThrowsAsync<NotFoundException>(() => _callers.RequireAsync(_studio.Id, CancellationToken.None));

        _currentUser.UserId = _client;
        await Assert.ThrowsAsync<NotFoundException>(() => _callers.RequireAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task ARouteThatAsksForAMember_StaysClosedToAClient()
    {
        _currentUser.UserId = _client;
        var members = new WorkspaceAccess(_workspaces, _currentUser);

        await Assert.ThrowsAsync<NotFoundException>(() => members.RequireMemberAsync(_studio.Id, CancellationToken.None));
    }

    [Fact]
    public async Task NotSignedIn_IsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => _callers.RequireAsync(_studio.Id, CancellationToken.None));
    }
}
