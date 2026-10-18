using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.ProjectUpdates;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class ProjectUpdateServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeProjectUpdateRepository _updates = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _workspace;
    private readonly Project _project;
    private readonly ProjectUpdateService _service;

    public ProjectUpdateServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _project = Project.Create(_workspace.Id, Guid.NewGuid(), "Website");
        _projects.Items.Add(_project);
        _currentUser = new FakeCurrentUser(_me);

        _service = new ProjectUpdateService(_updates, _projects, new WorkspaceAccess(_workspaces, _currentUser), _unitOfWork);
    }

    [Fact]
    public async Task Create_StartsAsDraft_AndSavesOnce()
    {
        var response = await _service.CreateAsync(_workspace.Id, _project.Id, Save(), CancellationToken.None);

        Assert.Equal("Draft", response.Status);
        Assert.Null(response.SharedAtUtc);
        Assert.Single(_updates.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_InAnArchivedProject_ThrowsConflict()
    {
        _project.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, Save(), CancellationToken.None));

        Assert.Empty(_updates.Items);
    }

    [Fact]
    public async Task Create_InAProjectOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);
        var theirs = Project.Create(other.Id, Guid.NewGuid(), "Theirs");
        _projects.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, theirs.Id, Save(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, Save(), CancellationToken.None));
    }

    [Fact]
    public async Task List_HidesArchivedUnlessAskedFor_AndOnlyShowsThisProject()
    {
        var kept = AddUpdate();
        AddUpdate().Archive();
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        _projects.Items.Add(other);
        _updates.Items.Add(ProjectUpdate.Create(_workspace.Id, other.Id, "Elsewhere", null));

        var active = await _service.ListAsync(_workspace.Id, _project.Id, includeArchived: false, CancellationToken.None);
        var all = await _service.ListAsync(_workspace.Id, _project.Id, includeArchived: true, CancellationToken.None);

        Assert.Equal(kept.Id, Assert.Single(active).Id);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Update_ChangesTheDetails_EvenWhenShared()
    {
        var update = AddUpdate();
        update.Share();

        var response = await _service.UpdateAsync(
            _workspace.Id,
            _project.Id,
            update.Id,
            new SaveProjectUpdateRequest { Title = "Design approved", Body = "Starting the build." },
            CancellationToken.None);

        Assert.Equal("Design approved", response.Title);
        Assert.Equal("Starting the build.", response.Body);
        Assert.Equal("Shared", response.Status);
    }

    [Fact]
    public async Task Update_AnArchivedUpdate_ThrowsConflict_AndChangesNothing()
    {
        var update = AddUpdate();
        update.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, update.Id, Save("Changed"), CancellationToken.None));

        Assert.Equal("Design done", update.Title);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_AnUpdateOfAnotherProject_ThrowsNotFound()
    {
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        _projects.Items.Add(other);
        var theirs = ProjectUpdate.Create(_workspace.Id, other.Id, "Theirs", null);
        _updates.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, theirs.Id, Save("Hijacked"), CancellationToken.None));

        Assert.Equal("Theirs", theirs.Title);
    }

    [Fact]
    public async Task ShareThenUnshare_SwitchesTheStatus_AndKeepsTheShareDate()
    {
        var update = AddUpdate();

        var shared = await _service.ShareAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None);
        var draft = await _service.UnshareAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None);

        Assert.Equal("Shared", shared.Status);
        Assert.NotNull(shared.SharedAtUtc);
        Assert.Equal("Draft", draft.Status);
        Assert.Equal(shared.SharedAtUtc, draft.SharedAtUtc);
    }

    [Fact]
    public async Task Share_AnArchivedUpdate_ThrowsConflict()
    {
        var update = AddUpdate();
        update.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ShareAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None));

        Assert.Equal(ProjectUpdateStatus.Draft, update.Status);
    }

    [Fact]
    public async Task ArchiveThenRestore_AsOwner_ComesBackAsDraft()
    {
        var update = AddUpdate();
        update.Share();

        var archived = await _service.ArchiveAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None);
        var restored = await _service.RestoreAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None);

        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Equal("Draft", archived.Status);
        Assert.Null(restored.ArchivedAtUtc);
        Assert.Equal("Draft", restored.Status);
    }

    [Fact]
    public async Task ArchiveAndRestore_AsMember_ThrowForbidden()
    {
        var member = Guid.NewGuid();
        _workspace.AddMember(member, WorkspaceRole.Member);
        var update = AddUpdate();
        _currentUser.UserId = member;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ArchiveAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RestoreAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None));

        Assert.False(update.IsArchived);
    }

    [Fact]
    public async Task Delete_ADraft_RemovesIt()
    {
        var update = AddUpdate();

        await _service.DeleteAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None);

        Assert.Empty(_updates.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Delete_ASharedUpdate_ThrowsConflict_AndKeepsIt()
    {
        var update = AddUpdate();
        update.Share();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.DeleteAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None));

        Assert.Single(_updates.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Changes_InAnArchivedProject_ThrowConflict_AndChangeNothing()
    {
        var update = AddUpdate();
        _project.Archive();
        var ct = CancellationToken.None;

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, update.Id, Save("Changed"), ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.ShareAsync(_workspace.Id, _project.Id, update.Id, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.ArchiveAsync(_workspace.Id, _project.Id, update.Id, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(_workspace.Id, _project.Id, update.Id, ct));

        Assert.Equal("Design done", update.Title);
        Assert.Equal(ProjectUpdateStatus.Draft, update.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Reading_InAnArchivedProject_StillWorks()
    {
        var update = AddUpdate();
        _project.Archive();

        var list = await _service.ListAsync(_workspace.Id, _project.Id, includeArchived: false, CancellationToken.None);
        var one = await _service.GetByIdAsync(_workspace.Id, _project.Id, update.Id, CancellationToken.None);

        Assert.Single(list);
        Assert.Equal(update.Id, one.Id);
    }

    private static SaveProjectUpdateRequest Save(string title = "Design done") => new() { Title = title };

    private ProjectUpdate AddUpdate()
    {
        var update = ProjectUpdate.Create(_workspace.Id, _project.Id, "Design done", null);
        _updates.Items.Add(update);
        return update;
    }
}
