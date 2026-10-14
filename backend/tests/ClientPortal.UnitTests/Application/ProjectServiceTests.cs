using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Mappings;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class ProjectServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeClientInvitationRepository _invitations = new();
    private readonly FakeIdentityService _identity = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _workspace;
    private readonly Client _acme;
    private readonly ProjectService _service;

    public ProjectServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _acme = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(_acme);
        _currentUser = new FakeCurrentUser(_me);

        var clientResponses = new ClientResponseBuilder(_identity, _invitations, new FakeTimeProvider());
        _service = new ProjectService(
            _projects,
            _clients,
            new ProjectResponseBuilder(_clients, clientResponses),
            new WorkspaceAccess(_workspaces, _currentUser),
            _unitOfWork);
    }

    [Fact]
    public async Task Create_ForAClientOfTheWorkspace_StartsInPlanning_AndSavesOnce()
    {
        var response = await _service.CreateAsync(_workspace.Id, NewProject(_acme.Id), CancellationToken.None);

        Assert.Equal("Planning", response.Status);
        Assert.Equal(new ProjectClientResponse(_acme.Id, "Acme"), response.Client);
        Assert.Single(_projects.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_ShowsTheEmail_WhenTheClientHasNoName()
    {
        var unnamed = Client.Register(_workspace.Id, null, "just@email.com");
        _clients.Items.Add(unnamed);

        var response = await _service.CreateAsync(_workspace.Id, NewProject(unnamed.Id), CancellationToken.None);

        Assert.Equal("just@email.com", response.Client.DisplayName);
    }

    [Fact]
    public async Task Create_ShowsTheAccountName_WhenTheClientHasAnAccount()
    {
        var account = _identity.AddAccount("contact@acme.com", "secret-password", "Elena from Acme");
        _acme.LinkTo(account.Id, DateTime.UtcNow);

        var response = await _service.CreateAsync(_workspace.Id, NewProject(_acme.Id), CancellationToken.None);

        Assert.Equal("Elena from Acme", response.Client.DisplayName);
    }

    [Fact]
    public async Task Create_ForAClientOfAnotherWorkspace_ThrowsNotFound_AndDoesNotSave()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);
        var theirs = Client.Register(other.Id, "Theirs", "theirs@example.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, NewProject(theirs.Id), CancellationToken.None));

        Assert.Empty(_projects.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_ForAnArchivedClient_ThrowsConflict()
    {
        _acme.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(_workspace.Id, NewProject(_acme.Id), CancellationToken.None));

        Assert.Empty(_projects.Items);
    }

    [Fact]
    public async Task Create_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, NewProject(_acme.Id), CancellationToken.None));
    }

    [Fact]
    public async Task List_FiltersByClient_AndHidesArchivedProjectsUnlessAskedFor()
    {
        var bright = Client.Register(_workspace.Id, "Bright", "bright@example.com");
        _clients.Items.Add(bright);
        var website = AddProject(_acme.Id, "Website");
        var archived = AddProject(_acme.Id, "Old site");
        archived.Archive();
        AddProject(bright.Id, "Menu");

        var acmeActive = await _service.ListAsync(_workspace.Id, includeArchived: false, _acme.Id, CancellationToken.None);
        var acmeAll = await _service.ListAsync(_workspace.Id, includeArchived: true, _acme.Id, CancellationToken.None);
        var everyone = await _service.ListAsync(_workspace.Id, includeArchived: false, clientId: null, CancellationToken.None);

        Assert.Equal(website.Id, Assert.Single(acmeActive).Id);
        Assert.Equal(2, acmeAll.Count);
        Assert.Equal(2, everyone.Count);
    }

    [Fact]
    public async Task Update_ChangesTheDetailsAndTheStatus()
    {
        var project = AddProject(_acme.Id, "Website");

        var response = await _service.UpdateAsync(
            _workspace.Id,
            project.Id,
            new UpdateProjectRequest { Name = "New website", Description = "Five pages", DueDate = new DateOnly(2026, 12, 1), Status = "Active" },
            CancellationToken.None);

        Assert.Equal("New website", response.Name);
        Assert.Equal("Active", response.Status);
        Assert.Equal(ProjectStatus.Active, project.Status);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_AnArchivedProject_ThrowsConflict_AndChangesNothing()
    {
        var project = AddProject(_acme.Id, "Website");
        project.Archive();

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(
            _workspace.Id,
            project.Id,
            new UpdateProjectRequest { Name = "Changed", Status = "Active" },
            CancellationToken.None));

        Assert.Equal("Website", project.Name);
        Assert.Equal(ProjectStatus.Planning, project.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_AProjectOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);
        var theirs = Project.Create(other.Id, Guid.NewGuid(), "Theirs");
        _projects.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(
            _workspace.Id,
            theirs.Id,
            new UpdateProjectRequest { Name = "Hijacked", Status = "Active" },
            CancellationToken.None));
    }

    [Fact]
    public async Task ArchiveThenRestore_AsOwner_TogglesTheArchivedDate()
    {
        var project = AddProject(_acme.Id, "Website");

        var archived = await _service.ArchiveAsync(_workspace.Id, project.Id, CancellationToken.None);
        var restored = await _service.RestoreAsync(_workspace.Id, project.Id, CancellationToken.None);

        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Null(restored.ArchivedAtUtc);
    }

    [Fact]
    public async Task Archive_AsMember_ThrowsForbidden()
    {
        var member = Guid.NewGuid();
        _workspace.AddMember(member, WorkspaceRole.Member);
        var project = AddProject(_acme.Id, "Website");
        _currentUser.UserId = member;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ArchiveAsync(_workspace.Id, project.Id, CancellationToken.None));

        Assert.False(project.IsArchived);
    }

    [Fact]
    public async Task AddStage_AppendsAStage_AndAnswersWithTheWholeProject()
    {
        var project = AddProject(_acme.Id, "Website");

        await _service.AddStageAsync(_workspace.Id, project.Id, new CreateStageRequest { Name = "Design" }, CancellationToken.None);
        var response = await _service.AddStageAsync(
            _workspace.Id,
            project.Id,
            new CreateStageRequest { Name = "Launch", DueDate = new DateOnly(2026, 12, 15) },
            CancellationToken.None);

        Assert.Equal(["Design", "Launch"], response.Stages.Select(s => s.Name));
        Assert.Equal(new ProjectProgressResponse(0, 2), response.Progress);
        Assert.Equal(2, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task UpdateStage_MarkingAStageDone_MovesTheProgress()
    {
        var project = AddProject(_acme.Id, "Website");
        var design = project.AddStage("Design", null, null);
        project.AddStage("Launch", null, null);

        var response = await _service.UpdateStageAsync(
            _workspace.Id,
            project.Id,
            design.Id,
            new UpdateStageRequest { Name = "Design", Status = "Done" },
            CancellationToken.None);

        Assert.Equal(new ProjectProgressResponse(1, 2), response.Progress);
        Assert.Equal("Done", response.Stages[0].Status);
    }

    [Fact]
    public async Task MoveStage_ReordersTheStages()
    {
        var project = AddProject(_acme.Id, "Website");
        project.AddStage("Design", null, null);
        var launch = project.AddStage("Launch", null, null);

        var response = await _service.MoveStageAsync(
            _workspace.Id,
            project.Id,
            launch.Id,
            new MoveStageRequest { Direction = "up" },
            CancellationToken.None);

        Assert.Equal(["Launch", "Design"], response.Stages.Select(s => s.Name));
    }

    [Fact]
    public async Task RemoveStage_DeletesIt()
    {
        var project = AddProject(_acme.Id, "Website");
        var design = project.AddStage("Design", null, null);

        var response = await _service.RemoveStageAsync(_workspace.Id, project.Id, design.Id, CancellationToken.None);

        Assert.Empty(response.Stages);
    }

    [Fact]
    public async Task StageChanges_OnAnArchivedProject_ThrowConflict_AndChangeNothing()
    {
        var project = AddProject(_acme.Id, "Website");
        var design = project.AddStage("Design", null, null);
        project.Archive();
        var ct = CancellationToken.None;

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.AddStageAsync(_workspace.Id, project.Id, new CreateStageRequest { Name = "New" }, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateStageAsync(
            _workspace.Id, project.Id, design.Id, new UpdateStageRequest { Name = "Changed", Status = "Done" }, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.MoveStageAsync(
            _workspace.Id, project.Id, design.Id, new MoveStageRequest { Direction = "down" }, ct));
        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.RemoveStageAsync(_workspace.Id, project.Id, design.Id, ct));

        Assert.Equal("Design", Assert.Single(project.Stages).Name);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task StageOfAnotherProject_IsNotFound()
    {
        var project = AddProject(_acme.Id, "Website");
        var other = AddProject(_acme.Id, "Other");
        var foreign = other.AddStage("Design", null, null);
        var ct = CancellationToken.None;

        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateStageAsync(
            _workspace.Id, project.Id, foreign.Id, new UpdateStageRequest { Name = "Hijacked", Status = "Done" }, ct));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.MoveStageAsync(
            _workspace.Id, project.Id, foreign.Id, new MoveStageRequest { Direction = "up" }, ct));
        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.RemoveStageAsync(_workspace.Id, project.Id, foreign.Id, ct));

        Assert.Equal("Design", Assert.Single(other.Stages).Name);
    }

    [Fact]
    public async Task StageChanges_InAWorkspaceIAmNotAMemberOf_ThrowNotFound()
    {
        var project = AddProject(_acme.Id, "Website");
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.AddStageAsync(_workspace.Id, project.Id, new CreateStageRequest { Name = "Design" }, CancellationToken.None));

        Assert.Empty(project.Stages);
    }

    private static CreateProjectRequest NewProject(Guid clientId) => new() { ClientId = clientId, Name = "Website" };

    private Project AddProject(Guid clientId, string name)
    {
        var project = Project.Create(_workspace.Id, clientId, name);
        _projects.Items.Add(project);
        return project;
    }
}
