using Partnr.Workspaces.Application.Authorization;
using Partnr.Workspaces.Application.DTOs.Meetings;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Mappings;
using Partnr.Workspaces.Application.Services;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;
using Partnr.Workspaces.Domain.Exceptions;

namespace Partnr.Workspaces.UnitTests.Application;

public class MeetingServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeProjectRepository _projects = new();
    private readonly FakeGoogleConnectionRepository _google = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Workspace _workspace;
    private readonly Client _acme;
    private readonly MeetingService _service;
    private readonly DateTimeOffset _tomorrow;

    public MeetingServiceTests()
    {
        _workspace = Workspace.Create("Studio", _me);
        _workspaces.Items.Add(_workspace);
        _acme = Client.Register(_workspace.Id, "Acme", "contact@acme.com");
        _clients.Items.Add(_acme);
        _google.Items.Add(GoogleConnection.Connect(_me, "ana@gmail.example", "sealed", _time.Now.UtcDateTime));
        _currentUser = new FakeCurrentUser(_me);
        _tomorrow = _time.Now.AddDays(1);

        var clientResponses = new ClientResponseBuilder(new FakeIdentityService(), new FakeClientInvitationRepository(), _time);
        var access = new WorkspaceAccess(_workspaces, _currentUser);
        var clientAccess = new ClientAccess(_projects, _workspaces);
        _service = new MeetingService(
            _meetings,
            _clients,
            _projects,
            _google,
            new MeetingResponseBuilder(_clients, clientResponses, _projects),
            access,
            clientAccess,
            new CallerAccess(access, clientAccess),
            _unitOfWork,
            _time);
    }

    [Fact]
    public async Task Create_SavesOnce_AndNamesTheClient()
    {
        var response = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);

        Assert.Equal("Scheduled", response.Status);
        Assert.Equal("Kick-off", response.Title);
        Assert.Equal("Acme", response.Client.DisplayName);
        Assert.Null(response.Project);
        Assert.Equal(_tomorrow.UtcDateTime, response.StartsAtUtc);
        var saved = Assert.Single(_meetings.Items);
        Assert.Equal(_me, saved.OrganizerUserId);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Create_StoresTheTimeInUtc_WhateverTheOffsetSent()
    {
        var request = Create() with { StartsAt = new DateTimeOffset(_tomorrow.UtcDateTime).ToOffset(TimeSpan.FromHours(3)), EndsAt = _tomorrow.AddHours(1).ToOffset(TimeSpan.FromHours(3)) };

        var response = await _service.CreateAsync(_workspace.Id, request, CancellationToken.None);

        Assert.Equal(_tomorrow.UtcDateTime, response.StartsAtUtc);
        Assert.Equal(DateTimeKind.Utc, _meetings.Items[0].StartsAtUtc.Kind);
    }

    [Fact]
    public async Task Create_AboutAProjectOfTheClient_NamesTheProject()
    {
        var project = Project.Create(_workspace.Id, _acme.Id, "Website");
        _projects.Items.Add(project);

        var response = await _service.CreateAsync(_workspace.Id, Create(project.Id), CancellationToken.None);

        Assert.Equal(new MeetingProjectResponse(project.Id, "Website"), response.Project);
    }

    [Fact]
    public async Task Create_AboutAnotherClientsProject_ThrowsConflict()
    {
        var other = Client.Register(_workspace.Id, "Other", "other@example.com");
        _clients.Items.Add(other);
        var theirs = Project.Create(_workspace.Id, other.Id, "Theirs");
        _projects.Items.Add(theirs);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(_workspace.Id, Create(theirs.Id), CancellationToken.None));
        Assert.Empty(_meetings.Items);
    }

    [Fact]
    public async Task Create_AboutAnArchivedProject_ThrowsConflict()
    {
        var project = Project.Create(_workspace.Id, _acme.Id, "Website");
        project.Archive();
        _projects.Items.Add(project);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(_workspace.Id, Create(project.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Create_AboutAProjectOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);
        var theirs = Project.Create(other.Id, Guid.NewGuid(), "Theirs");
        _projects.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(_workspace.Id, Create(theirs.Id), CancellationToken.None));
    }

    [Fact]
    public async Task Create_ForAClientOfAnotherWorkspace_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);
        var theirs = Client.Register(other.Id, "Theirs", "theirs@example.com");
        _clients.Items.Add(theirs);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(_workspace.Id, Create() with { ClientId = theirs.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task Create_ForAnArchivedClient_ThrowsConflict()
    {
        _acme.Archive();

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_WithoutAConnectedGoogleAccount_AsksToConnectFirst()
    {
        _google.Items.Clear();
        var notConnected = await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None));
        Assert.Contains("Connect your Google account", notConnected.Message);

        var lapsed = GoogleConnection.Connect(_me, "ana@gmail.example", "sealed", _time.Now.UtcDateTime);
        lapsed.MarkNeedsReconnect();
        _google.Items.Add(lapsed);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None));

        Assert.Empty(_meetings.Items);
    }

    [Fact]
    public async Task Create_WithAnotherMembersGoogleAccountConnected_StillNeedsYourOwn()
    {
        _google.Items.Clear();
        _google.Items.Add(GoogleConnection.Connect(Guid.NewGuid(), "someone@gmail.example", "sealed", _time.Now.UtcDateTime));

        await Assert.ThrowsAsync<ConflictException>(() => _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_InThePast_ThrowsDomainException()
    {
        var request = Create() with { StartsAt = _time.Now.AddHours(-1), EndsAt = _time.Now.AddMinutes(-30) };

        await Assert.ThrowsAsync<DomainException>(() => _service.CreateAsync(_workspace.Id, request, CancellationToken.None));
    }

    [Fact]
    public async Task Create_InAWorkspaceIAmNotAMemberOf_ThrowsNotFound()
    {
        _currentUser.UserId = Guid.NewGuid();

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None));
    }

    [Fact]
    public async Task Create_Anonymous_ThrowsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None));
    }

    [Fact]
    public async Task List_IsSoonestFirst_HidesCancelledUnlessAsked_AndFiltersByClientAndProject()
    {
        var project = Project.Create(_workspace.Id, _acme.Id, "Website");
        _projects.Items.Add(project);
        var other = Client.Register(_workspace.Id, "Other", "other@example.com");
        _clients.Items.Add(other);
        var later = await _service.CreateAsync(_workspace.Id, Create() with { Title = "Later", StartsAt = _tomorrow.AddDays(2), EndsAt = _tomorrow.AddDays(2).AddHours(1) }, CancellationToken.None);
        var sooner = await _service.CreateAsync(_workspace.Id, Create(project.Id) with { Title = "Sooner" }, CancellationToken.None);
        var cancelled = await _service.CreateAsync(_workspace.Id, Create() with { Title = "Cancelled", StartsAt = _tomorrow.AddDays(1), EndsAt = _tomorrow.AddDays(1).AddHours(1) }, CancellationToken.None);
        await _service.CancelAsync(_workspace.Id, cancelled.Id, CancellationToken.None);
        await _service.CreateAsync(_workspace.Id, Create() with { ClientId = other.Id, Title = "Theirs" }, CancellationToken.None);

        var all = (await _service.ListAsync(_workspace.Id, false, _acme.Id, null, CancellationToken.None)).Member!;
        Assert.Equal([sooner.Id, later.Id], all.Select(m => m.Id));
        Assert.Equal(3, (await _service.ListAsync(_workspace.Id, true, _acme.Id, null, CancellationToken.None)).Member!.Count);
        Assert.Equal([sooner.Id], (await _service.ListAsync(_workspace.Id, false, null, project.Id, CancellationToken.None)).Member!.Select(m => m.Id));
        Assert.Equal(3, (await _service.ListAsync(_workspace.Id, false, null, null, CancellationToken.None)).Member!.Count);
    }

    [Fact]
    public async Task GetById_OfAnotherWorkspacesMeeting_ThrowsNotFound()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(other.Id, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Update_ReplacesTheDetails_AndKeepsTheOrganiser()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        var colleague = Guid.NewGuid();
        _workspace.AddMember(colleague, WorkspaceRole.Member);
        _currentUser.UserId = colleague;

        var changed = await _service.UpdateAsync(
            _workspace.Id,
            created.Id,
            new SaveMeetingRequest { Title = "Moved", Description = "New time", StartsAt = _tomorrow.AddHours(3), EndsAt = _tomorrow.AddHours(4) },
            CancellationToken.None);

        Assert.Equal("Moved", changed.Title);
        Assert.Equal(_tomorrow.AddHours(3).UtcDateTime, changed.StartsAtUtc);
        Assert.Equal(_me, _meetings.Items[0].OrganizerUserId);
    }

    [Fact]
    public async Task Update_OfACancelledMeeting_OrOneThatEnded_ThrowsConflict()
    {
        var cancelled = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        await _service.CancelAsync(_workspace.Id, cancelled.Id, CancellationToken.None);
        var ended = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        _time.Now = _tomorrow.AddHours(2);

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(_workspace.Id, cancelled.Id, Save(), CancellationToken.None));
        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(_workspace.Id, ended.Id, Save(), CancellationToken.None));
    }

    [Fact]
    public async Task Update_WhenTheClientWasArchived_ThrowsConflict()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        _acme.Archive();

        await Assert.ThrowsAsync<ConflictException>(() => _service.UpdateAsync(_workspace.Id, created.Id, Save(), CancellationToken.None));
    }

    [Fact]
    public async Task Update_ToAProjectOfAnotherClient_ThrowsConflict()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        var other = Client.Register(_workspace.Id, "Other", "other@example.com");
        _clients.Items.Add(other);
        var theirs = Project.Create(_workspace.Id, other.Id, "Theirs");
        _projects.Items.Add(theirs);

        await Assert.ThrowsAsync<ConflictException>(() =>
            _service.UpdateAsync(_workspace.Id, created.Id, Save() with { ProjectId = theirs.Id }, CancellationToken.None));
    }

    [Fact]
    public async Task Update_DoesNotNeedGoogleAgain_SoAMeetingCanStillBeFixedAfterTheAccountLapsed()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        _google.Items.Clear();

        var changed = await _service.UpdateAsync(_workspace.Id, created.Id, Save() with { Title = "Renamed" }, CancellationToken.None);

        Assert.Equal("Renamed", changed.Title);
    }

    [Fact]
    public async Task Cancel_KeepsItInTheHistory_AndASecondCancelIsAConflict()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);

        var cancelled = await _service.CancelAsync(_workspace.Id, created.Id, CancellationToken.None);

        Assert.Equal("Cancelled", cancelled.Status);
        Assert.NotNull(cancelled.CancelledAtUtc);
        Assert.Single(_meetings.Items);
        await Assert.ThrowsAsync<ConflictException>(() => _service.CancelAsync(_workspace.Id, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task Cancel_AMeetingThatAlreadyTookPlace_ThrowsConflict()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        _time.Now = _tomorrow.AddHours(2);

        await Assert.ThrowsAsync<ConflictException>(() => _service.CancelAsync(_workspace.Id, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task EveryChange_QueuesTheMeetingForGoogle()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        Assert.Equal(("Pending", null), (created.SyncStatus, created.MeetUrl));
        _meetings.Items[0].RecordSynced(1, "event", "https://meet.google.com/abc", Now());

        var changed = await _service.UpdateAsync(_workspace.Id, created.Id, Save() with { Title = "Renamed" }, CancellationToken.None);
        Assert.Equal(("Pending", "https://meet.google.com/abc"), (changed.SyncStatus, changed.MeetUrl));
        _meetings.Items[0].RecordSynced(2, null, null, Now());

        var cancelled = await _service.CancelAsync(_workspace.Id, created.Id, CancellationToken.None);
        Assert.Equal("Pending", cancelled.SyncStatus);
    }

    [Fact]
    public async Task RetrySync_AfterTheTriesRanOut_QueuesItAgain()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        for (var i = 0; i < 5; i++)
        {
            _meetings.Items[0].RecordSyncFailure(1, Now());
        }

        var response = await _service.RetrySyncAsync(_workspace.Id, created.Id, CancellationToken.None);

        Assert.Equal("Pending", response.SyncStatus);
        Assert.Equal(0, _meetings.Items[0].SyncAttempts);
    }

    [Fact]
    public async Task RetrySync_WhenThereIsNothingToRetry_OrTheMeetingIsOver_ThrowsConflict()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        await Assert.ThrowsAsync<ConflictException>(() => _service.RetrySyncAsync(_workspace.Id, created.Id, CancellationToken.None));

        for (var i = 0; i < 5; i++)
        {
            _meetings.Items[0].RecordSyncFailure(1, Now());
        }

        _time.Now = _tomorrow.AddHours(2);
        await Assert.ThrowsAsync<ConflictException>(() => _service.RetrySyncAsync(_workspace.Id, created.Id, CancellationToken.None));
    }

    [Fact]
    public async Task RetrySync_OfAnotherWorkspacesMeeting_ThrowsNotFound()
    {
        var created = await _service.CreateAsync(_workspace.Id, Create(), CancellationToken.None);
        var other = Workspace.Create("Other", _me);
        _workspaces.Items.Add(other);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.RetrySyncAsync(other.Id, created.Id, CancellationToken.None));
    }

    private DateTime Now() => _time.Now.UtcDateTime;

    private CreateMeetingRequest Create(Guid? projectId = null) => new()
    {
        ClientId = _acme.Id,
        ProjectId = projectId,
        Title = "Kick-off",
        StartsAt = _tomorrow,
        EndsAt = _tomorrow.AddHours(1),
    };

    private SaveMeetingRequest Save() => new() { Title = "Kick-off", StartsAt = _tomorrow, EndsAt = _tomorrow.AddHours(1) };
}
