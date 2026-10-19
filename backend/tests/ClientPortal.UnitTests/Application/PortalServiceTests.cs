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
    private readonly FakeDeliverableRepository _deliverables = new();
    private readonly FakeProjectUpdateRepository _updates = new();
    private readonly FakeProjectFileRepository _files = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeApprovalRequestRepository _approvals = new();
    private readonly FakeCurrentUser _currentUser = new();
    private readonly PortalService _service;

    public PortalServiceTests()
    {
        _projects = new FakeProjectRepository { Clients = _clients };
        _service = new PortalService(
            _workspaces,
            _projects,
            _deliverables,
            _updates,
            _files,
            _approvals,
            _storage,
            new WorkspaceAccess(_workspaces, _currentUser));
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
    public async Task GetProject_ShowsOnlySharedDeliverables_WithTheirStageName_NewestFirst()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        var design = project.AddStage("Design", null, null);
        var older = AddDeliverable(project, "Older", design.Id, shared: true);
        Thread.Sleep(2); // CreatedAtUtc decides the order
        var newer = AddDeliverable(project, "Newer", stageId: null, shared: true);
        AddDeliverable(project, "Still a draft", null, shared: false);
        AddDeliverable(project, "Archived", null, shared: true).Archive();

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        Assert.Equal([newer.Id, older.Id], response.Deliverables.Select(d => d.Id));
        Assert.Null(response.Deliverables[0].StageName);
        Assert.Equal("Design", response.Deliverables[1].StageName);
    }

    [Fact]
    public async Task GetProject_NeverShowsTheDeliverablesOfAnotherProject()
    {
        var (studio, client) = AsClientOfAStudio();
        var mine = AddProject(studio, client, "Website", ProjectStatus.Active);
        var other = AddProject(studio, client, "Other", ProjectStatus.Active);
        AddDeliverable(other, "Theirs", null, shared: true);

        var response = await _service.GetProjectAsync(mine.Id, CancellationToken.None);

        Assert.Empty(response.Deliverables);
    }

    [Fact]
    public async Task GetProject_ShowsOnlySharedUpdates_MostRecentlySharedFirst()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        var first = AddUpdate(project, "First", shared: true);
        Thread.Sleep(2); // SharedAtUtc decides the order
        var second = AddUpdate(project, "Second", shared: true);
        AddUpdate(project, "Still a draft", shared: false);
        AddUpdate(project, "Archived", shared: true).Archive();
        var withdrawn = AddUpdate(project, "Withdrawn", shared: true);
        withdrawn.Unshare();

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        Assert.Equal([second.Id, first.Id], response.Updates.Select(u => u.Id));
        Assert.Equal(("Second", "Some details"), (response.Updates[0].Title, response.Updates[0].Body));
        Assert.Equal(second.SharedAtUtc, response.Updates[0].SharedAtUtc);
    }

    [Fact]
    public async Task GetProject_NeverShowsTheUpdatesOfAnotherProject()
    {
        var (studio, client) = AsClientOfAStudio();
        var mine = AddProject(studio, client, "Website", ProjectStatus.Active);
        var other = AddProject(studio, client, "Other", ProjectStatus.Active);
        AddUpdate(other, "Theirs", shared: true);

        var response = await _service.GetProjectAsync(mine.Id, CancellationToken.None);

        Assert.Empty(response.Updates);
    }

    [Fact]
    public async Task GetProject_ShowsOnlySharedFiles_NewestFirst_WithoutAnythingInternal()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        var first = AddFile(project, "first.pdf", shared: true);
        Thread.Sleep(2); // CreatedAtUtc decides the order
        var second = AddFile(project, "second.fig", shared: true);
        AddFile(project, "draft.pdf", shared: false);
        AddFile(project, "archived.pdf", shared: true).Archive();
        AddFile(project, "withdrawn.pdf", shared: true).Unshare();

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        Assert.Equal([second.Id, first.Id], response.Files.Select(f => f.Id));
        Assert.Equal((second.Name, second.Description, "second.fig", 4), (response.Files[0].Name, response.Files[0].Description, response.Files[0].FileName, response.Files[0].SizeBytes));
    }

    [Fact]
    public async Task GetProject_NeverShowsTheFilesOfAnotherProject()
    {
        var (studio, client) = AsClientOfAStudio();
        var mine = AddProject(studio, client, "Website", ProjectStatus.Active);
        var other = AddProject(studio, client, "Other", ProjectStatus.Active);
        AddFile(other, "theirs.pdf", shared: true);

        var response = await _service.GetProjectAsync(mine.Id, CancellationToken.None);

        Assert.Empty(response.Files);
    }

    [Fact]
    public async Task DownloadFile_OfASharedFile_ReturnsTheBytesWithTheFileNameAndType()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        var file = AddFile(project, "mockups.pdf", shared: true);

        var download = await _service.DownloadFileAsync(project.Id, file.Id, CancellationToken.None);

        using var content = download.Content;
        using var reader = new StreamReader(content);
        Assert.Equal("data", await reader.ReadToEndAsync());
        Assert.Equal(("mockups.pdf", "application/pdf", 4), (download.FileName, download.ContentType, download.Length));
    }

    [Fact]
    public async Task DownloadFile_ThatIsNotSharedForTheClient_IsNotFound()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        var draft = AddFile(project, "draft.pdf", shared: false);
        var archived = AddFile(project, "archived.pdf", shared: true);
        archived.Archive();
        var other = AddProject(studio, client, "Other", ProjectStatus.Active);
        var elsewhere = AddFile(other, "elsewhere.pdf", shared: true);

        foreach (var id in new[] { draft.Id, archived.Id, elsewhere.Id, Guid.NewGuid() })
        {
            await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadFileAsync(project.Id, id, CancellationToken.None));
        }
    }

    [Fact]
    public async Task DownloadFile_OfAProjectInPlanningOrArchivedOrSomeoneElses_IsNotFound()
    {
        var (studio, client) = AsClientOfAStudio();
        var planning = AddProject(studio, client, "Soon", ProjectStatus.Planning);
        var planned = AddFile(planning, "planned.pdf", shared: true);
        var archivedProject = AddProject(studio, client, "Old", ProjectStatus.Active);
        var old = AddFile(archivedProject, "old.pdf", shared: true);
        archivedProject.Archive();
        var strangersStudio = Workspace.Create("Not mine", Guid.NewGuid());
        _workspaces.Items.Add(strangersStudio);
        var strangersClient = Client.Register(strangersStudio.Id, "Other", "other@example.com");
        _clients.Items.Add(strangersClient);
        var theirs = AddProject(strangersStudio, strangersClient, "Theirs", ProjectStatus.Active);
        var secret = AddFile(theirs, "secret.pdf", shared: true);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadFileAsync(planning.Id, planned.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadFileAsync(archivedProject.Id, old.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.DownloadFileAsync(theirs.Id, secret.Id, CancellationToken.None));
    }

    [Fact]
    public async Task DownloadFile_Anonymous_ThrowsUnauthorized() =>
        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.DownloadFileAsync(Guid.NewGuid(), Guid.NewGuid(), CancellationToken.None));

    [Fact]
    public async Task GetProject_ShowsTheOpenRound_AndTheDecidedOnesNewestFirst_ButNeverAWithdrawnOne()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        var mockup = AddDeliverable(project, "Mockup", null, shared: true);
        var changes = AddRound(project, mockup, "Check the logo.");
        changes.RequestChanges(_me, "Bigger logo.");
        AddRound(project, mockup, "Withdrawn one").Withdraw();
        var approved = AddRound(project, mockup, "Second try.");
        approved.Approve(_me, "Looks great.");
        var open = AddRound(project, mockup, "Final check.");

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        var approval = Assert.Single(response.Deliverables).Approval;
        Assert.Equal("Pending", approval.Status);
        Assert.Equal((open.Id, "Final check."), (approval.Pending!.Id, approval.Pending.Note));
        Assert.Equal(["Approved", "ChangesRequested"], approval.History.Select(h => h.Decision));
        Assert.Equal(("Second try.", "Looks great."), (approval.History[0].Note, approval.History[0].Message));
        Assert.Equal(("Check the logo.", "Bigger logo."), (approval.History[1].Note, approval.History[1].Message));
        Assert.DoesNotContain("Withdrawn one", approval.History.Select(h => h.Note));
    }

    [Fact]
    public async Task GetProject_WithoutRounds_ShowsNoApproval()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        AddDeliverable(project, "Mockup", null, shared: true);

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        var approval = Assert.Single(response.Deliverables).Approval;
        Assert.Equal("None", approval.Status);
        Assert.Null(approval.Pending);
        Assert.Empty(approval.History);
        Assert.Equal(0, response.AwaitingApproval);
    }

    [Fact]
    public async Task GetProject_WithAWithdrawnRoundOnly_LooksLikeNothingWasAsked()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        var mockup = AddDeliverable(project, "Mockup", null, shared: true);
        AddRound(project, mockup, "Never mind").Withdraw();

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        Assert.Equal("None", response.Deliverables[0].Approval.Status);
        Assert.Empty(response.Deliverables[0].Approval.History);
        Assert.Equal(0, response.AwaitingApproval);
    }

    [Fact]
    public async Task GetProject_CountsHowManyDeliverablesWaitForTheClient()
    {
        var (studio, client) = AsClientOfAStudio();
        var project = AddProject(studio, client, "Website", ProjectStatus.Active);
        AddRound(project, AddDeliverable(project, "Waiting one", null, shared: true), null);
        AddRound(project, AddDeliverable(project, "Waiting two", null, shared: true), null);
        AddRound(project, AddDeliverable(project, "Answered", null, shared: true), null).Approve(_me, null);
        AddDeliverable(project, "Nothing asked", null, shared: true);

        var response = await _service.GetProjectAsync(project.Id, CancellationToken.None);

        Assert.Equal(2, response.AwaitingApproval);
    }

    [Fact]
    public async Task ListProjects_ShowsHowManyThingsWaitForTheClient_PerProject()
    {
        var (studio, client) = AsClientOfAStudio();
        var busy = AddProject(studio, client, "Busy", ProjectStatus.Active);
        var quiet = AddProject(studio, client, "Quiet", ProjectStatus.Active);
        AddRound(busy, AddDeliverable(busy, "One", null, shared: true), null);
        AddRound(busy, AddDeliverable(busy, "Two", null, shared: true), null);
        AddRound(quiet, AddDeliverable(quiet, "Done", null, shared: true), null).Approve(_me, null);

        var list = await _service.ListProjectsAsync(CancellationToken.None);

        Assert.Equal(2, list.Single(p => p.Name == "Busy").AwaitingApproval);
        Assert.Equal(0, list.Single(p => p.Name == "Quiet").AwaitingApproval);
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

    private Deliverable AddDeliverable(Project project, string title, Guid? stageId, bool shared)
    {
        var deliverable = Deliverable.Create(project.WorkspaceId, project.Id, stageId, title, null, "https://example.com/file");
        if (shared)
        {
            deliverable.Share();
        }

        _deliverables.Items.Add(deliverable);

        return deliverable;
    }

    private ApprovalRequest AddRound(Project project, Deliverable deliverable, string? note)
    {
        var round = ApprovalRequest.Request(project.WorkspaceId, project.Id, deliverable.Id, Guid.NewGuid(), note);
        _approvals.Items.Add(round);

        return round;
    }

    private ProjectUpdate AddUpdate(Project project, string title, bool shared)
    {
        var update = ProjectUpdate.Create(project.WorkspaceId, project.Id, title, "Some details");
        if (shared)
        {
            update.Share();
        }

        _updates.Items.Add(update);

        return update;
    }

    private ProjectFile AddFile(Project project, string fileName, bool shared)
    {
        var file = ProjectFile.Create(project.WorkspaceId, project.Id, null, "Some notes", fileName, 4);
        if (shared)
        {
            file.Share();
        }

        _files.Items.Add(file);
        _storage.Objects[file.StorageKey] = "data"u8.ToArray();

        return file;
    }

    private Project AddProject(Workspace studio, Client client, string name, ProjectStatus status, string? description = null)
    {
        var project = Project.Create(studio.Id, client.Id, name, description);
        project.ChangeStatus(status);
        _projects.Items.Add(project);

        return project;
    }
}
