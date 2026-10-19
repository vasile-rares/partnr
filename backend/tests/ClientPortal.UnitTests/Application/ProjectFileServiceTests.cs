using System.Text;
using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.ProjectFiles;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Services;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.UnitTests.Application;

public class ProjectFileServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeProjectFileRepository _files = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeFileStorage _storage = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly Workspace _workspace;
    private readonly Project _project;
    private readonly ProjectFileService _service;

    public ProjectFileServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _project = Project.Create(_workspace.Id, Guid.NewGuid(), "Website");
        _projects.Items.Add(_project);
        _currentUser = new FakeCurrentUser(_me);

        _service = new ProjectFileService(_files, _projects, _storage, new WorkspaceAccess(_workspaces, _currentUser), _unitOfWork);
    }

    [Fact]
    public async Task Create_StoresTheBytesUnderAnIdKey_AndStartsAsDraft()
    {
        var response = await _service.CreateAsync(_workspace.Id, _project.Id, new UploadProjectFileRequest(), Upload("mockups.pdf", "hello"), CancellationToken.None);

        var file = Assert.Single(_files.Items);
        Assert.Equal("Draft", response.Status);
        Assert.Equal("mockups.pdf", response.Name);
        Assert.Equal(5, response.SizeBytes);
        Assert.Equal("application/pdf", response.ContentType);
        Assert.Equal("hello", Encoding.UTF8.GetString(_storage.Objects[file.StorageKey]));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_WithADisallowedType_StoresNothing()
    {
        await Assert.ThrowsAsync<DomainException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, new UploadProjectFileRequest(), Upload("setup.exe", "MZ"), CancellationToken.None));

        Assert.Empty(_files.Items);
        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task Create_WhenSavingTheRowFails_RemovesTheStoredBytes()
    {
        var service = new ProjectFileService(
            _files,
            _projects,
            _storage,
            new WorkspaceAccess(_workspaces, _currentUser),
            new FailingUnitOfWork());

        await Assert.ThrowsAsync<ConflictException>(() =>
            service.CreateAsync(_workspace.Id, _project.Id, new UploadProjectFileRequest(), Upload("a.pdf", "x"), CancellationToken.None));

        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task Create_InAnArchivedProject_ThrowsConflict_AndStoresNothing()
    {
        _project.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, new UploadProjectFileRequest(), Upload("a.pdf", "x"), CancellationToken.None));

        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task Create_InAProjectOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);
        var theirs = Project.Create(other.Id, Guid.NewGuid(), "Theirs");
        _projects.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, theirs.Id, new UploadProjectFileRequest(), Upload("a.pdf", "x"), CancellationToken.None));
    }

    [Fact]
    public async Task Create_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.CreateAsync(_workspace.Id, _project.Id, new UploadProjectFileRequest(), Upload("a.pdf", "x"), CancellationToken.None));

        Assert.Empty(_storage.Objects);
    }

    [Fact]
    public async Task List_HidesArchivedUnlessAskedFor_AndOnlyShowsThisProject()
    {
        var kept = await AddFile("kept.pdf");
        (await AddFile("gone.pdf")).Archive();
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        _projects.Items.Add(other);
        _files.Items.Add(ProjectFile.Create(_workspace.Id, other.Id, null, null, "elsewhere.pdf", 1));

        var active = await _service.ListAsync(_workspace.Id, _project.Id, includeArchived: false, CancellationToken.None);
        var all = await _service.ListAsync(_workspace.Id, _project.Id, includeArchived: true, CancellationToken.None);

        Assert.Equal(kept.Id, Assert.Single(active).Id);
        Assert.Equal(2, all.Count);
    }

    [Fact]
    public async Task Download_ReturnsTheStoredBytesWithTheFileNameAndType()
    {
        var file = await AddFile("mockups.pdf", "pdf-bytes");

        var download = await _service.DownloadAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None);
        using var content = download.Content;

        using var reader = new StreamReader(content);
        Assert.Equal("pdf-bytes", await reader.ReadToEndAsync());
        Assert.Equal(("mockups.pdf", "application/pdf", 9), (download.FileName, download.ContentType, download.Length));
    }

    [Fact]
    public async Task Download_AFileOfAnotherProject_ThrowsNotFound()
    {
        var other = Project.Create(_workspace.Id, Guid.NewGuid(), "Other");
        _projects.Items.Add(other);
        var theirs = ProjectFile.Create(_workspace.Id, other.Id, null, null, "theirs.pdf", 1);
        _files.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() =>
            _service.DownloadAsync(_workspace.Id, _project.Id, theirs.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Download_InAnArchivedProject_StillWorks()
    {
        var file = await AddFile("mockups.pdf");
        _project.Archive();

        var download = await _service.DownloadAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None);
        using var content = download.Content;

        Assert.Equal("mockups.pdf", download.FileName);
    }

    [Fact]
    public async Task Update_ChangesNameAndDescription_EvenWhenShared_AndNeverTouchesTheFile()
    {
        var file = await AddFile("mockups.pdf", "bytes");
        file.Share();

        var response = await _service.UpdateAsync(
            _workspace.Id,
            _project.Id,
            file.Id,
            new SaveProjectFileRequest { Name = "Final mockups", Description = "Round 2" },
            CancellationToken.None);

        Assert.Equal(("Final mockups", "Round 2", "Shared"), (response.Name, response.Description, response.Status));
        Assert.Equal("mockups.pdf", response.FileName);
        Assert.Equal("bytes", Encoding.UTF8.GetString(_storage.Objects[file.StorageKey]));
    }

    [Fact]
    public async Task Update_AnArchivedFile_ThrowsConflict_AndChangesNothing()
    {
        var file = await AddFile("mockups.pdf");
        file.Archive();
        var saves = _unitOfWork.SaveCount;

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, file.Id, new SaveProjectFileRequest { Name = "Changed" }, CancellationToken.None));

        Assert.Equal("mockups.pdf", file.Name);
        Assert.Equal(saves, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ShareThenUnshare_SwitchesTheStatus()
    {
        var file = await AddFile("mockups.pdf");

        var shared = await _service.ShareAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None);
        var draft = await _service.UnshareAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None);

        Assert.Equal("Shared", shared.Status);
        Assert.Equal("Draft", draft.Status);
    }

    [Fact]
    public async Task Share_AnArchivedFile_ThrowsConflict()
    {
        var file = await AddFile("mockups.pdf");
        file.Archive();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.ShareAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None));

        Assert.Equal(ProjectFileStatus.Draft, file.Status);
    }

    [Fact]
    public async Task ArchiveThenRestore_AsOwner_ComesBackAsDraft_AndKeepsTheBytes()
    {
        var file = await AddFile("mockups.pdf");
        file.Share();

        var archived = await _service.ArchiveAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None);
        var restored = await _service.RestoreAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None);

        Assert.NotNull(archived.ArchivedAtUtc);
        Assert.Equal("Draft", archived.Status);
        Assert.Null(restored.ArchivedAtUtc);
        Assert.Equal("Draft", restored.Status);
        Assert.True(_storage.Objects.ContainsKey(file.StorageKey));
    }

    [Fact]
    public async Task ArchiveAndRestore_AsMember_ThrowForbidden()
    {
        var member = Guid.NewGuid();
        _workspace.AddMember(member, WorkspaceRole.Member);
        var file = await AddFile("mockups.pdf");
        _currentUser.UserId = member;

        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.ArchiveAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None));
        await Assert.ThrowsAsync<ForbiddenException>(() =>
            _service.RestoreAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None));

        Assert.False(file.IsArchived);
    }

    [Fact]
    public async Task Delete_ADraft_RemovesTheRowAndTheBytes()
    {
        var file = await AddFile("mockups.pdf");
        var saves = _unitOfWork.SaveCount;

        await _service.DeleteAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None);

        Assert.Empty(_files.Items);
        Assert.Empty(_storage.Objects);
        Assert.Equal(saves + 1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Delete_ASharedFile_ThrowsConflict_AndKeepsEverything()
    {
        var file = await AddFile("mockups.pdf");
        file.Share();

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.DeleteAsync(_workspace.Id, _project.Id, file.Id, CancellationToken.None));

        Assert.Single(_files.Items);
        Assert.Single(_storage.Objects);
    }

    [Fact]
    public async Task Changes_InAnArchivedProject_ThrowConflict_AndChangeNothing()
    {
        var file = await AddFile("mockups.pdf");
        _project.Archive();
        var saves = _unitOfWork.SaveCount;
        var ct = CancellationToken.None;

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(_workspace.Id, _project.Id, file.Id, new SaveProjectFileRequest { Name = "Changed" }, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.ShareAsync(_workspace.Id, _project.Id, file.Id, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.ArchiveAsync(_workspace.Id, _project.Id, file.Id, ct));
        await Assert.ThrowsAsync<ConflictException>(() => _service.DeleteAsync(_workspace.Id, _project.Id, file.Id, ct));

        Assert.Equal("mockups.pdf", file.Name);
        Assert.Equal(ProjectFileStatus.Draft, file.Status);
        Assert.Equal(saves, _unitOfWork.SaveCount);
        Assert.Single(_storage.Objects);
    }

    private static FileUpload Upload(string fileName, string content)
    {
        var bytes = Encoding.UTF8.GetBytes(content);
        return new FileUpload(fileName, bytes.Length, new MemoryStream(bytes));
    }

    private async Task<ProjectFile> AddFile(string fileName, string content = "x")
    {
        await _service.CreateAsync(_workspace.Id, _project.Id, new UploadProjectFileRequest(), Upload(fileName, content), CancellationToken.None);
        return _files.Items[^1];
    }

    private sealed class FailingUnitOfWork : ClientPortal.Application.Interfaces.Repositories.IUnitOfWork
    {
        public Task<int> SaveChangesAsync(CancellationToken cancellationToken = default) =>
            throw new ConflictException("A record with the same unique value already exists.");
    }
}
