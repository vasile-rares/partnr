using Partnr.Core.Application.Authorization;
using Partnr.Core.Application.DTOs.Meetings;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Mappings;
using Partnr.Core.Application.Services;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.UnitTests.Application;

/// <summary>The meetings list as a client gets it from the one MeetingService, and what stays closed to them.</summary>
public class MeetingServiceForClientsTests
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
    private readonly MeetingService _service;
    private readonly DateTime _now;

    public MeetingServiceForClientsTests()
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

        var access = new WorkspaceAccess(_workspaces, _currentUser);
        var clientAccess = new ClientAccess(_projects, _workspaces);
        var clientResponses = new ClientResponseBuilder(new FakeIdentityService(), new FakeClientInvitationRepository(), _time);
        _service = new MeetingService(
            _meetings,
            _clients,
            _projects,
            new FakeGoogleConnectionStatus(),
            new FakeEventPublisher(),
            new MeetingResponseBuilder(_clients, clientResponses, _projects),
            access,
            clientAccess,
            new CallerAccess(access, clientAccess),
            new FakeUnitOfWork(),
            _time);
    }

    [Fact]
    public async Task List_ShowsMyMeetings_SoonestFirst_WithoutTheirOrganiserOrClientCard()
    {
        var later = Schedule(_mine, days: 3, title: "Later");
        var sooner = Schedule(_mine, days: 1, title: "Sooner");

        var list = await ListAsync();

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

        Assert.Equal([mine.Id], (await ListAsync()).Select(m => m.Id));

        _mine.Archive();
        Assert.Empty(await ListAsync());
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

        var list = await ListAsync();

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

        var list = await ListAsync();

        Assert.Equal([lastWeek.Id, recent.Id], list.Select(m => m.Id));
        Assert.Equal("Cancelled", list[1].Status);
    }

    [Fact]
    public async Task List_OfAFirmIAmNotAClientOf_ThrowsNotFound()
    {
        var other = Workspace.Create("Other", Guid.NewGuid());
        _workspaces.Items.Add(other);

        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync(other.Id));
        await Assert.ThrowsAsync<NotFoundException>(() => ListAsync(Guid.NewGuid()));
    }

    [Fact]
    public async Task List_Anonymous_ThrowsUnauthorized()
    {
        _currentUser.UserId = null;

        await Assert.ThrowsAsync<UnauthorizedException>(() => ListAsync());
    }

    [Fact]
    public async Task List_AsAMemberOfTheFirm_IsTheFirmsOwnShape_NotTheClients()
    {
        _currentUser.UserId = _organizer;
        Schedule(_mine, days: 1, title: "Kick-off");

        var answer = await _service.ListAsync(_studio.Id, false, null, null, CancellationToken.None);

        Assert.NotNull(answer.Member);
        Assert.Null(answer.Client);
        Assert.IsAssignableFrom<IReadOnlyList<MeetingResponse>>(answer.Body);
    }

    [Fact]
    public async Task EverythingButTheList_StaysClosedToAClient()
    {
        var meeting = Schedule(_mine, days: 1, title: "Kick-off");
        var request = new CreateMeetingRequest { ClientId = _mine.Id, Title = "x", StartsAt = _time.Now.AddDays(2), EndsAt = _time.Now.AddDays(2).AddHours(1) };

        await Assert.ThrowsAsync<NotFoundException>(() => _service.CreateAsync(_studio.Id, request, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.GetByIdAsync(_studio.Id, meeting.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.UpdateAsync(_studio.Id, meeting.Id, request, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.CancelAsync(_studio.Id, meeting.Id, CancellationToken.None));
        await Assert.ThrowsAsync<NotFoundException>(() => _service.RetrySyncAsync(_studio.Id, meeting.Id, CancellationToken.None));
    }

    private async Task<IReadOnlyList<ClientMeetingResponse>> ListAsync(Guid? workspaceId = null) =>
        (await _service.ListAsync(workspaceId ?? _studio.Id, false, null, null, CancellationToken.None)).Client!;

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
