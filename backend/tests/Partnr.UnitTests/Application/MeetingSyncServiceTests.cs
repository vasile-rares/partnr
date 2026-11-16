using Partnr.Application.Interfaces.Services;
using Partnr.Application.Services;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.UnitTests.Application;

public class MeetingSyncServiceTests
{
    private readonly Guid _organizer = Guid.NewGuid();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeClientRepository _clients = new();
    private readonly FakeGoogleConnectionRepository _connections = new();
    private readonly FakeGoogleCalendarClient _google = new();
    private readonly FakeSecretProtector _protector = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly Client _acme;
    private readonly GoogleConnection _connection;
    private readonly MeetingSyncService _service;

    public MeetingSyncServiceTests()
    {
        _acme = Client.Register(Guid.NewGuid(), "Acme", "contact@acme.com");
        _clients.Items.Add(_acme);
        _connection = GoogleConnection.Connect(
            _organizer,
            "ana@gmail.example",
            _protector.Protect(GoogleConnectionService.RefreshTokenPurpose, "refresh-1"),
            Now());
        _connections.Items.Add(_connection);

        _service = new MeetingSyncService(_meetings, _clients, _connections, _google, _protector, _unitOfWork, _time);
    }

    [Fact]
    public async Task ANewMeeting_IsCreatedInGoogle_WithTheClientInvited_AndTheMeetLinkKept()
    {
        var meeting = NewMeeting(description: "Bring the brief");

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(new MeetingSyncRun(1, 0, 0), run);
        var call = Assert.Single(_google.Calls);
        Assert.Equal(("upsert", "refresh-1", meeting.EventId, false), (call.Operation, call.Token, call.EventId, call.Exists));
        Assert.Equal(new GoogleEventDetails("Kick-off", "Bring the brief", meeting.StartsAtUtc, meeting.EndsAtUtc, "contact@acme.com"), call.Details);
        Assert.Equal(MeetingSyncStatus.Synced, meeting.SyncStatus);
        Assert.Equal(meeting.EventId, meeting.GoogleEventId);
        Assert.StartsWith("https://meet.google.com/", meeting.MeetUrl);
        Assert.Null(meeting.NextSyncAtUtc);
    }

    [Fact]
    public async Task AMeetingThatIsInStep_IsNotSentAgain()
    {
        NewMeeting();
        await _service.SyncDueAsync(CancellationToken.None);

        var again = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(new MeetingSyncRun(0, 0, 0), again);
        Assert.Single(_google.Calls);
    }

    [Fact]
    public async Task AChangedMeeting_UpdatesTheEventInsteadOfCreatingAnother()
    {
        var meeting = NewMeeting();
        await _service.SyncDueAsync(CancellationToken.None);

        meeting.Update(null, "Moved", null, meeting.StartsAtUtc.AddHours(2), meeting.EndsAtUtc.AddHours(2), Now());
        await _service.SyncDueAsync(CancellationToken.None);

        var update = _google.Calls[1];
        Assert.True(update.Exists);
        Assert.Equal("Moved", update.Details!.Title);
        Assert.Equal(MeetingSyncStatus.Synced, meeting.SyncStatus);
    }

    [Fact]
    public async Task ACancelledMeeting_DeletesItsEvent_SoGoogleTellsTheClient()
    {
        var meeting = NewMeeting();
        await _service.SyncDueAsync(CancellationToken.None);

        meeting.Cancel(Now());
        await _service.SyncDueAsync(CancellationToken.None);

        var delete = _google.Calls[1];
        Assert.Equal(("delete", meeting.EventId), (delete.Operation, delete.EventId));
        Assert.Equal(MeetingSyncStatus.Synced, meeting.SyncStatus);
    }

    [Fact]
    public async Task AMeetingCancelledBeforeItReachedGoogle_NeedsNoCallAtAll()
    {
        var meeting = NewMeeting();
        meeting.Cancel(Now());

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(1, run.Synced);
        Assert.Empty(_google.Calls);
        Assert.Equal(MeetingSyncStatus.Synced, meeting.SyncStatus);
        Assert.Null(meeting.GoogleEventId);
    }

    [Fact]
    public async Task WhenGoogleFails_ItIsTriedAgainAfterOneFiveFifteenAndSixtyMinutes_ThenGivesUp()
    {
        var meeting = NewMeeting();
        _google.Throw = new GoogleCalendarException("down");
        var delays = new List<TimeSpan>();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var run = await _service.SyncDueAsync(CancellationToken.None);
            Assert.Equal(1, run.Failed);
            Assert.Equal(MeetingSyncStatus.Pending, meeting.SyncStatus);
            Assert.Equal(attempt, meeting.SyncAttempts);
            delays.Add(meeting.NextSyncAtUtc!.Value - Now());
            _time.Now = new DateTimeOffset(meeting.NextSyncAtUtc.Value, TimeSpan.Zero);
        }

        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)], delays);

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Failed, meeting.SyncStatus);
        Assert.Null(meeting.NextSyncAtUtc);
        Assert.Equal(5, _google.Calls.Count);
        Assert.Equal(new MeetingSyncRun(0, 0, 0), await _service.SyncDueAsync(CancellationToken.None));
    }

    [Fact]
    public async Task AFailedTry_IsNotRepeatedBeforeItsTime()
    {
        NewMeeting();
        _google.Throw = new GoogleCalendarException("down");
        await _service.SyncDueAsync(CancellationToken.None);

        _time.Now = _time.Now.AddSeconds(30);
        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Single(_google.Calls);
    }

    [Fact]
    public async Task AMeetingThatIsOver_IsNotTriedAgain()
    {
        var meeting = NewMeeting();
        _google.Throw = new GoogleCalendarException("down");
        _time.Now = new DateTimeOffset(meeting.EndsAtUtc.AddMinutes(1), TimeSpan.Zero);

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Failed, meeting.SyncStatus);
    }

    [Fact]
    public async Task WhenGoogleRefusesThePermission_TheMeetingAndTheConnectionWaitForAReconnect_WithoutRetrying()
    {
        var meeting = NewMeeting();
        _google.Throw = new GoogleAccessRevokedException("revoked");

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(new MeetingSyncRun(0, 0, 1), run);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, meeting.SyncStatus);
        Assert.Equal(GoogleConnectionStatus.NeedsReconnect, _connection.Status);

        // Nothing is tried for this organiser until they connect again.
        var another = NewMeeting();
        _google.Throw = null;
        await _service.SyncDueAsync(CancellationToken.None);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, another.SyncStatus);
        Assert.Single(_google.Calls);
    }

    [Fact]
    public async Task WithoutAConnection_TheMeetingWaitsForOne()
    {
        var meeting = NewMeeting();
        _connections.Items.Clear();

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(1, run.NeedReconnect);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, meeting.SyncStatus);
        Assert.Empty(_google.Calls);
    }

    [Fact]
    public async Task ASavedTokenThatCannotBeOpened_IsTreatedLikeARevokedOne()
    {
        var meeting = NewMeeting();
        _connections.Items.Clear();
        _connections.Items.Add(GoogleConnection.Connect(_organizer, "ana@gmail.example", "not-ours", Now()));

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.NeedsReconnect, meeting.SyncStatus);
        Assert.Equal(GoogleConnectionStatus.NeedsReconnect, _connections.Items[0].Status);
        Assert.Empty(_google.Calls);
    }

    [Fact]
    public async Task AChangeMadeWhileGoogleAnswers_IsNotMarkedDone_ButTheEventIsKept()
    {
        var meeting = NewMeeting();
        _google.DuringCall = () => meeting.Update(null, "Changed meanwhile", null, meeting.StartsAtUtc, meeting.EndsAtUtc, Now());

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Pending, meeting.SyncStatus);
        Assert.Equal(meeting.EventId, meeting.GoogleEventId);
        Assert.Equal(Now(), meeting.NextSyncAtUtc);

        _google.DuringCall = null;
        await _service.SyncDueAsync(CancellationToken.None);

        var second = _google.Calls[1];
        Assert.True(second.Exists);
        Assert.Equal("Changed meanwhile", second.Details!.Title);
        Assert.Equal(MeetingSyncStatus.Synced, meeting.SyncStatus);
    }

    [Fact]
    public async Task OneMeetingFailing_NeverStopsTheOthers()
    {
        var first = NewMeeting(title: "First");
        var second = NewMeeting(title: "Second");
        var calls = 0;
        _google.DuringCall = () =>
        {
            if (++calls == 1)
            {
                throw new GoogleCalendarException("down");
            }
        };

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(new MeetingSyncRun(1, 1, 0), run);
        Assert.Equal(MeetingSyncStatus.Pending, first.SyncStatus);
        Assert.Equal(MeetingSyncStatus.Synced, second.SyncStatus);
    }

    private DateTime Now() => _time.Now.UtcDateTime;

    private Meeting NewMeeting(string title = "Kick-off", string? description = null)
    {
        var start = Now().AddDays(1);
        var meeting = Meeting.Schedule(_acme.WorkspaceId, _acme.Id, _organizer, null, title, description, start, start.AddHours(1), Now());
        _meetings.Items.Add(meeting);

        return meeting;
    }
}
