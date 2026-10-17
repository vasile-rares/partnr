using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.UnitTests.Application;

public class DeliverableServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeDeliverableRepository _deliverables = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _workspace;
    private readonly Project _project;
    private readonly DeliverableService _service;

    public DeliverableServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _project = Project.Create(_workspace.Id, Guid.NewGuid(), "Website");
        _projects.Items.Add(_project);
        _currentUser = new FakeCurrentUser(_me);

        _service = new DeliverableService(_deliverables, _projects, new WorkspaceAccess(_workspaces, _currentUser), _unitOfWork);
    }

    [Fact]
    public async Task Create_StartsAsDraft_NamesTheStage_AndSavesOnce()
    {
        var design = _project.AddStage("Design", null, null);

        var response = await _service.CreateAsync(_workspace.Id, _project.Id, Save(design.Id), CancellationToken.None);

        Assert.Equal("Draft", response.Status);
        Assert.Equal(design.Id, response.StageId);
        Assert.Equal("Design", response.StageName);
        Assert.Single(_deliverables.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithoutAStage_IsAllowed()
    {
        var response = await _service.CreateAsync(_workspace.Id, _project.Id, Save(null), CancellationToken.None);

        Assert.Null(response.StageId);
        Assert.Null(response.StageName);
    }

    [Fact]
    public async Task Create_WithAStageOfAnotherProject_ThrowsNotFound_AndDoesNotSave()
    {
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        var foreign = other.AddStage("Design", null, null);
        _projects.Items.Add(other);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, Save(foreign.Id), CancellationToken.None));

        Assert.Empty(_deliverables.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_InAnArchivedProject_ThrowsConflict()
    {
        _project.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, Save(null), CancellationToken.None));

        Assert.Empty(_deliverables.Items);
    }

    [Fact]
    public async Task Create_InAProjectOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);
        var theirs = Project.Create(other.Id, Guid.NewGuid(), "Theirs");
        _projects.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, theirs.Id, Save(null), CancellationToken.None));
    }

    [Fact]
    public async Task Create_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, Save(null), CancellationToken.None));
    }

    [Fact]
    public async Task List_HidesArchivedUnlessAskedFor_AndOnlyShowsThisProject()
    {
        var kept = AddDeliverable();
        AddDeliverable().Archive();
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        _projects.Items.Add(other);
        _deliverables.Items.Add(Deliverable.Create(_workspace.Id, other.Id, null, "Elsewhere", null, "https://example.com"));

        var active = await _service.ListAsync(_workspace.Id, _project.Id, includeArchived: false, CancellationToken.None);
        var all = await _service.ListAsync(_workspace.Id, _project.Id, includeArchived: true, CancellationToken.None);

        Assert.Equal(kept.Id, Assert.Single(active).Id);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Update_ChangesTheDetails_EvenWhenShared()
    {
        var deliverable = AddDeliverable();
        deliverable.Share();
        var design = _project.AddStage("Design", null, null);

        var response = await _service.UpdateAsync(
            _workspace.Id,
            _project.Id,
            deliverable.Id,
            new SaveDeliverableRequest { StageId = design.Id, Title = "Final mockup", Url = "https://figma.com/v2" },
            CancellationToken.None);

        Assert.Equal("Final mockup", response.Title);
        Assert.Equal("Design", response.StageName);
        Assert.Equal("Shared", response.Status);
    }

    [Fact]
    public async Task Update_AnArchivedDeliverable_ThrowsConflict_AndChangesNothing()
    {
        var deliverable = AddDeliverable();
        deliverable.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, deliverable.Id, Save(null, "Changed"), CancellationToken.None));

        Assert.Equal("Mockup", deliverable.Title);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Update_ADeliverableOfAnotherProject_ThrowsNotFound()
    {
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        _projects.Items.Add(other);
        var theirs = Deliverable.Create(_workspace.Id, other.Id, null, "Theirs", null, "https://example.com");
        _deliverables.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, theirs.Id, Save(null, "Hijacked"), CancellationToken.None));

        Assert.Equal("Theirs", theirs.Title);
    }

    [Fact]
    public async Task ShareThenUnshare_SwitchesTheStatus()
    {
        var deliverable = AddDeliverable();

        var shared = await _service.ShareAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None);
        var draft = await _service.UnshareAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None);

        Assert.Equal("Shared", shared.Status);
        Assert.Equal("Draft", draft.Status);
    }

    [Fact]
    public async Task Share_AnArchivedDeliverable_ThrowsConflict()
    {
        var deliverable = AddDeliverable();
        deliverable.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ShareAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None));

        Assert.Equal(DeliverableStatus.Draft, deliverable.Status);
    }

    [Fact]
    public async Task ArchiveThenRestore_AsOwner_ComesBackAsDraft()
    {
        var deliverable = AddDeliverable();
        deliverable.Share();

        var archived = await _service.ArchiveAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None);
        var restored = await _service.RestoreAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None);

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
        var deliverable = AddDeliverable();
        _currentUser.UserId = member;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ArchiveAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RestoreAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None));

        Assert.False(deliverable.IsArchived);
    }

    [Fact]
    public async Task Delete_ADraft_RemovesIt()
    {
        var deliverable = AddDeliverable();

        await _service.DeleteAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None);

        Assert.Empty(_deliverables.Items);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Delete_ASharedDeliverable_ThrowsConflict_AndKeepsIt()
    {
        var deliverable = AddDeliverable();
        deliverable.Share();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.DeleteAsync(_workspace.Id, _project.Id, deliverable.Id, CancellationToken.None));

        Assert.Single(_deliverables.Items);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Changes_InAnArchivedProject_ThrowConflict_AndChangeNothing()
    {
        var deliverable = AddDeliverable();
        _project.Archive();
        var ct = CancellationToken.None;

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, deliverable.Id, Save(null, "Changed"), ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.ShareAsync(_workspace.Id, _project.Id, deliverable.Id, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.ArchiveAsync(_workspace.Id, _project.Id, deliverable.Id, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(_workspace.Id, _project.Id, deliverable.Id, ct));

        Assert.Equal("Mockup", deliverable.Title);
        Assert.Equal(DeliverableStatus.Draft, deliverable.Status);
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    private static SaveDeliverableRequest Save(Guid? stageId, string title = "Mockup") =>
        new() { StageId = stageId, Title = title, Url = "https://figma.com/file/abc" };

    private Deliverable AddDeliverable()
    {
        var deliverable = Deliverable.Create(_workspace.Id, _project.Id, null, "Mockup", null, "https://figma.com/file/abc");
        _deliverables.Items.Add(deliverable);
        return deliverable;
    }
}
