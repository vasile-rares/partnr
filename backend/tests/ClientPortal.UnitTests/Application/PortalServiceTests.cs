using ClientPortal.Application.Authorization;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class PortalServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeProjectRepository _projects;
    private readonly FakeCurrentUser _currentUser = new();
    private readonly PortalService _service;

    public PortalServiceTests()
    {
        _projects = new FakeProjectRepository { Clients = _clients };
        _service = new PortalService(_workspaces, _projects, new WorkspaceAccess(_workspaces, _currentUser));
    }

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

    [Fact]
    public async Task ListProjects_ShowsTheProjectsOutOfPlanning_WithTheirWorkspaceAndProgress()
    {
        var (studio, client) = AsClientOfAStudio();
        var site = AddProject(studio, client, "Website", ProjectStatus.Active);
        var design = site.AddStage("Design", null, null);
        site.AddStage("Launch", null, null);
        site.UpdateStage(design.Id, "Design", null, null, StageStatus.Done);
        AddProject(studio, client, "Paused one", ProjectStatus.OnHold);
        AddProject(studio, client, "Delivered", ProjectStatus.Completed);

        var list = await _service.ListProjectsAsync(CancellationToken.None);

        Assert.Equal(["Delivered", "Paused one", "Website"], list.Select(p => p.Name).Order());
        var shown = Assert.Single(list, p => p.Name == "Website");
        Assert.Equal(("Studio Pop", "Active"), (shown.Workspace.Name, shown.Status));
        Assert.Equal((1, 2), (shown.Progress.Done, shown.Progress.Total));
    }

    [Fact]
    public async Task ListProjects_LeavesOutDrafts_ArchivedProjects_AndProjectsOfAnArchivedClient()
    {
        var (studio, client) = AsClientOfAStudio();
        AddProject(studio, client, "Draft", ProjectStatus.Planning);
        AddProject(studio, client, "Archived", ProjectStatus.Active).Archive();
        var visible = AddProject(studio, client, "Visible", ProjectStatus.Active);

        Assert.Equal(visible.Id, Assert.Single(await _service.ListProjectsAsync(CancellationToken.None)).Id);

        client.Archive();
        Assert.Empty(await _service.ListProjectsAsync(CancellationToken.None));
    }

    [Fact]
    public async Task ListProjects_NeverShowsTheProjectsOfAnotherClient()
    {
        var (studio, mine) = AsClientOfAStudio();
        var someoneElse = Client.Register(studio.Id, "Someone", "someone@example.com");
        someoneElse.LinkTo(Guid.NewGuid(), DateTime.UtcNow);
        _clients.Items.Add(someoneElse);
        AddProject(studio, mine, "Mine", ProjectStatus.Active);
        AddProject(studio, someoneElse, "Theirs", ProjectStatus.Active);

        var list = await _service.ListProjectsAsync(CancellationToken.None);

        Assert.Equal("Mine", Assert.Single(list).Name);
    }

    [Fact]
    public async Task ListProjects_Anonymous_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.ListProjectsAsync(CancellationToken.None));

    [Fact]
    public async Task GetProject_ReturnsTheDetailsAndTheStagesInOrder()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active, "Five pages");
        project.AddStage("Design", "Layouts", new DateOnly(2026, 11, 5));
        project.AddStage("Launch", null, null);

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        Assert.Equal(("Website", "Five pages", "Studio Pop"), (response.Name, response.Description, response.Workspace.Name));
        Assert.Equal(["Design", "Launch"], response.Stages.Select(s => s.Name));
        Assert.Equal("Layouts", response.Stages[0].Description);
    }

    [Fact]
    public async Task GetProject_ThatIsADraft_Archived_OrSomeoneElses_LooksLikeItDoesNotExist()
    {
        var (studio, client) = AsClientOfAStudio();
        var draft = AddProject(studio, client, "Draft", ProjectStatus.Planning);
        var archived = AddProject(studio, client, "Archived", ProjectStatus.Active);
        archived.Archive();
        var other = Client.Register(studio.Id, "Someone", "someone@example.com");
        other.LinkTo(Guid.NewGuid(), DateTime.UtcNow);
        _clients.Items.Add(other);
        var theirs = AddProject(studio, other, "Theirs", ProjectStatus.Active);

        foreach (var id in new[] { draft.Id, archived.Id, theirs.Id, Guid.NewGuid() })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _service.GetProjectAsync(id, CancellationToken.None));
        }
    }

    [Fact]
    public async Task GetProject_Anonymous_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.GetProjectAsync(Guid.NewGuid(), CancellationToken.None));

    private (Workspace Studio, Client Client) AsClientOfAStudio()
    {
        var studio = Workspace.Create("Studio Pop", Guid.NewGuid());
        _workspaces.Items.Add(studio);
        _workspaces.ClientOf[_me] = [studio.Id];
        var client = Client.Register(studio.Id, "Elena", "elena@example.com");
        client.LinkTo(_me, DateTime.UtcNow);
        _clients.Items.Add(client);
        _currentUser.UserId = _me;

        return (studio, client);
    }

    private Project AddProject(Workspace studio, Client client, string name, ProjectStatus status, string? description = null)
    {
        var project = Project.Create(studio.Id, client.Id, name, description);
        project.ChangeStatus(status);
        _projects.Items.Add(project);

        return project;
    }
}
