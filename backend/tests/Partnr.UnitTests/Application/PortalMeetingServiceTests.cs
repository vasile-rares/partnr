using Partnr.Application.Authorization;
using Partnr.Application.DTOs.Meetings;
using Partnr.Application.Exceptions;
using Partnr.Application.Services;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.UnitTests.Application;

public class PortalMeetingServiceTests
{
    private readonly Guid _me = Guid.NewGuid();
    private readonly Guid _organizer = Guid.NewGuid();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeWorkspaceRepository _workspaces = new();
    private readonly FakeCurrentUser _currentUser;
    private readonly FakeTimeProvider _time = new();
    private readonly FakeMeetingRepository _meetings;
    private readonly FakeProjectRepository _projects;
    private readonly Workspace _studio;
    private readonly Client _mine;
    private readonly PortalMeetingService _service;
    private readonly DateTime _now;

    public PortalMeetingServiceTests()
    {
        _now = _time.Now.UtcDateTime;
        _currentUser = new FakeCurrentUser(_me);
        _meetings = new FakeMeetingRepository { Clients = _clients };
        _projects = new FakeProjectRepository { Clients = _clients };
        _studio = Workspace.Create("Studio Pop", _organizer);
        _workspaces.Items.Add(_studio);
        _workspaces.ClientOf[_me] = [_studio.Id];
        _mine = Client.Register(_studio.Id, "Elena", "elena@example.com");
        _mine.LinkTo(_me, _now);
        _clients.Items.Add(_mine);

        _service = new PortalMeetingService(_meetings, _projects, _workspaces, new WorkspaceAccess(_workspaces, _currentUser), _time);
    }

    [Fact]
    public async Task List_ShowsMyMeetings_SoonestFirst_WithoutTheirOrganiserOrClientCard()
    {
        var later = Schedule(_mine, days: 3, title: "Later");
        var sooner = Schedule(_mine, days: 1, title: "Sooner");

        var list = await _service.ListAsync(_studio.Id, CancellationToken.None);

        Assert.Equal([sooner.Id, later.Id], list.Select(m => m.Id));
        Assert.All(list, m => Assert.Equal("Scheduled", m.Status));
    }

    [Fact]
    public async Task List_HidesAnotherClientsMeetings_AndThoseOfAnArchivedCard()
    {
        var other = Client.Register(_studio.Id, "Other", "other@example.com");
        _clients.Items.Add(other);
        Schedule(other, days: 1, title: "Theirs");
        var mine = Schedule(_mine, days: 2, title: "Mine");

        Assert.Equal([mine.Id], (await _service.ListAsync(_studio.Id, CancellationToken.None)).Select(m => m.Id));

        _mine.Archive();
        Assert.Empty(await _service.ListAsync(_studio.Id, CancellationToken.None));
    }

    [Fact]
    public async Task List_NamesTheProjectOnlyWhenTheClientCanOpenIt()
    {
        var shared = Project.Create(_studio.Id, _mine.Id, "Website");
        shared.ChangeStatus(ProjectStatus.Active);
        var draft = Project.Create(_studio.Id, _mine.Id, "Secret plan"); // Planning: the client does not see it
        _projects.Items.AddRange([shared, draft]);
        Schedule(_mine, days: 1, title: "About the website", projectId: shared.Id);
        Schedule(_mine, days: 2, title: "About the secret", projectId: draft.Id);

        var list = await _service.ListAsync(_studio.Id, CancellationToken.None);

        Assert.Equal(new MeetingProjectResponse(shared.Id, "Website"), list[0].Project);
        Assert.Null(list[1].Project);
        Assert.DoesNotContain("Secret", string.Join(' ', list.Select(m => m.Project?.Name)));
    }

    [Fact]
    public async Task List_KeepsCancelledOnesForAMonth_ThenForgetsThem()
    {
        var recent = Schedule(_mine, days: 1, title: "Called off");
        recent.Cancel(_now);
        Schedule(_mine, days: -31, title: "Long ago", skipChecks: true);
        var lastWeek = Schedule(_mine, days: -7, title: "Last week", skipChecks: true);

        var list = await _service.ListAsync(_studio.Id, CancellationToken.None);

        Assert.Equal([lastWeek.Id, recent.Id], list.Select(m => m.Id));
        Assert.Equal("Cancelled", list[1].Status);
    }

    [Fact]
    public async Task List_OfAFirmIAmNotAClientOf_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", Guid.NewGuid());
        _workspaces.Items.Add(other);

        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(other.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.ListAsync(Guid.NewGuid(), CancellationToken.None));
    }

    [Fact]
    public async Task List_Anonymous_ThrowsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => _service.ListAsync(_studio.Id, CancellationToken.None));
    }

    // `skipChecks` writes a meeting that is already in the past, which Schedule would refuse: it goes through a clock set back.
    private Meeting Schedule(Client client, int days, string title, Guid? projectId = null, bool skipChecks = false)
    {
        var start = _now.AddDays(days);
        var meeting = Meeting.Schedule(
            _studio.Id,
            client.Id,
            _organizer,
            projectId,
            title,
            null,
            start,
            start.AddHours(1),
            skipChecks ? start.AddDays(-1) : _now);
        _meetings.Items.Add(meeting);

        return meeting;
    }
}
