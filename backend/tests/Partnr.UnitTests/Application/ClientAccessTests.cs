using Partnr.Application.Authorization;
using Partnr.Application.Exceptions;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.UnitTests.Application;

public class ClientAccessTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeProjectRepository _projects;
    private readonly ClientAccess _access;
    private readonly Workspace _studio = Workspace.Create("Studio Pop", Guid.NewGuid());
    private readonly Workspace _sideProject = Workspace.Create("Side Project", Guid.NewGuid());

    public ClientAccessTests()
    {
        _projects = new FakeProjectRepository { Clients = _clients };
        _access = new ClientAccess(_projects, _workspaces);
        _workspaces.Items.AddRange([_studio, _sideProject]);
        _workspaces.ClientOf[_me] = [_studio.Id, _sideProject.Id];
    }

    private Project AddProject(Workspace workspace, Guid userId, ProjectStatus status)
    {
        var client = Client.Register(workspace.Id, "Elena", $"{Guid.NewGuid()}@example.com");
        client.LinkTo(userId, DateTime.UtcNow);
        _clients.Items.Add(client);

        var project = Project.Create(workspace.Id, client.Id, "Website");
        project.ChangeStatus(status);
        _projects.Items.Add(project);

        return project;
    }

    [Fact]
    public async Task RequireProject_ReturnsAProjectSharedWithMe()
    {
        var project = AddProject(_studio, _me, ProjectStatus.Active);

        Assert.Same(project, await _access.RequireProjectAsync(_me, project.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RequireProject_IsNotFound_ForADraft_ForSomeoneElsesProject_AndForAnUnknownId()
    {
        var draft = AddProject(_studio, _me, ProjectStatus.Planning);
        var theirs = AddProject(_studio, Guid.NewGuid(), ProjectStatus.Active);

        foreach (var id in new[] { draft.Id, theirs.Id, Guid.NewGuid() })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _access.RequireProjectAsync(_me, id, CancellationToken.None));
        }
    }

    [Fact]
    public async Task RequireWorkspace_IsNotFound_WhenIAmNotAClientOfIt()
    {
        var other = Workspace.Create("Elsewhere", Guid.NewGuid());
        _workspaces.Items.Add(other);

        Assert.Same(_studio, await _access.RequireWorkspaceAsync(_me, _studio.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _access.RequireWorkspaceAsync(_me, other.Id, CancellationToken.None));
    }

    [Fact]
    public async Task ListProjects_CanBeNarrowedToOneFirm_AndNeverShowsADraft()
    {
        var inStudio = AddProject(_studio, _me, ProjectStatus.Active);
        var inSideProject = AddProject(_sideProject, _me, ProjectStatus.OnHold);
        AddProject(_studio, _me, ProjectStatus.Planning);

        var all = await _access.ListProjectsAsync(_me, workspaceId: null, CancellationToken.None);
        var onlyStudio = await _access.ListProjectsAsync(_me, _studio.Id, CancellationToken.None);

        Assert.Equal(new[] { inStudio.Id, inSideProject.Id }.Order(), all.Select(p => p.Id).Order());
        Assert.Equal([inStudio.Id], onlyStudio.Select(p => p.Id));
    }
}
