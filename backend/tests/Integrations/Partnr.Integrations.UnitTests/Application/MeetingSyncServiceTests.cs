using Partnr.Contracts.Events;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Application.Services;
using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.UnitTests.Application;

public class MeetingSyncServiceTests
{
    private readonly Guid _organizer = Guid.NewGuid();
    private readonly FakeMeetingSyncRepository _syncs = new();
    private readonly FakeGoogleConnectionRepository _connections = new();
    private readonly FakeGoogleCalendarClient _google = new();
    private readonly FakeSecretProtector _protector = new();
    private readonly FakeEventPublisher _events = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly GoogleConnection _connection;
    private readonly MeetingSyncService _service;

    public MeetingSyncServiceTests()
    {
        _connection = GoogleConnection.Connect(
            _organizer,
            "ana@gmail.example",
            _protector.Protect(GoogleConnectionService.RefreshTokenPurpose, "refresh-1"),
            Now());
        _connections.Items.Add(_connection);

        _service = new MeetingSyncService(_syncs, _connections, _google, _protector, _events, _unitOfWork, _time);
    }

    [Fact]
    public async Task ANewMeeting_IsCreatedInGoogle_WithTheClientInvited_TheMeetLinkKept_AndTheAnswerSent()
    {
        var row = NewRow(description: "Bring the brief");

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(new MeetingSyncRun(1, 0, 0), run);
        var call = Assert.Single(_google.Calls);
        Assert.Equal(("upsert", "refresh-1", row.EventId, false), (call.Operation, call.Token, call.EventId, call.Exists));
        Assert.Equal(new GoogleEventDetails("Kick-off", "Bring the brief", row.StartsAtUtc, row.EndsAtUtc, "contact@acme.com"), call.Details);
        Assert.Equal(MeetingSyncStatus.Synced, row.Status);
        Assert.Equal(row.EventId, row.GoogleEventId);
        Assert.StartsWith("https://meet.google.com/", row.MeetUrl);
        Assert.Null(row.NextSyncAtUtc);
        Assert.Equal(new MeetingSynced(row.Id, 1, MeetingSyncOutcome.Synced, row.MeetUrl), Assert.Single(_events.Published));
    }

    [Fact]
    public async Task ARowThatIsInStep_IsNotSentAgain()
    {
        NewRow();
        await _service.SyncDueAsync(CancellationToken.None);

        var again = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(new MeetingSyncRun(0, 0, 0), again);
        Assert.Single(_google.Calls);
        Assert.Single(_events.Published);
    }

    [Fact]
    public async Task AChangedMeeting_UpdatesTheEventInsteadOfCreatingAnother()
    {
        var row = NewRow();
        await _service.SyncDueAsync(CancellationToken.None);

        Change(row, title: "Moved", moveBy: TimeSpan.FromHours(2));
        await _service.SyncDueAsync(CancellationToken.None);

        var update = _google.Calls[1];
        Assert.True(update.Exists);
        Assert.Equal("Moved", update.Details!.Title);
        Assert.Equal(MeetingSyncStatus.Synced, row.Status);
        Assert.Equal(2, _events.Published.Count);
    }

    [Fact]
    public async Task ACancelledMeeting_DeletesItsEvent_SoGoogleTellsTheClient_AndNoMeetLinkIsSent()
    {
        var row = NewRow();
        await _service.SyncDueAsync(CancellationToken.None);

        Change(row, cancelled: true);
        await _service.SyncDueAsync(CancellationToken.None);

        var delete = _google.Calls[1];
        Assert.Equal(("delete", row.EventId), (delete.Operation, delete.EventId));
        Assert.Equal(MeetingSyncStatus.Synced, row.Status);
        Assert.Null(Assert.IsType<MeetingSynced>(_events.Published[1]).MeetUrl);
    }

    [Fact]
    public async Task AMeetingCancelledBeforeItReachedGoogle_NeedsNoCallAtAll()
    {
        var row = NewRow();
        Change(row, cancelled: true);

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(1, run.Synced);
        Assert.Empty(_google.Calls);
        Assert.Equal(MeetingSyncStatus.Synced, row.Status);
        Assert.Null(row.GoogleEventId);
    }

    [Fact]
    public async Task WhenGoogleFails_ItIsTriedAgainAfterOneFiveFifteenAndSixtyMinutes_ThenGivesUp_AndOnlyTheEndIsSent()
    {
        var row = NewRow();
        _google.Throw = new GoogleCalendarException("down");
        var delays = new List<TimeSpan>();

        for (var attempt = 1; attempt <= 4; attempt++)
        {
            var run = await _service.SyncDueAsync(CancellationToken.None);
            Assert.Equal(1, run.Failed);
            Assert.Equal(MeetingSyncStatus.Pending, row.Status);
            Assert.Equal(attempt, row.SyncAttempts);
            delays.Add(row.NextSyncAtUtc!.Value - Now());
            _time.Now = new DateTimeOffset(row.NextSyncAtUtc.Value, TimeSpan.Zero);
        }

        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)], delays);
        Assert.Empty(_events.Published);

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Failed, row.Status);
        Assert.Null(row.NextSyncAtUtc);
        Assert.Equal(5, _google.Calls.Count);
        Assert.Equal(new MeetingSyncRun(0, 0, 0), await _service.SyncDueAsync(CancellationToken.None));
        Assert.Equal(new MeetingSynced(row.Id, 1, MeetingSyncOutcome.Failed, null), Assert.Single(_events.Published));
    }

    [Fact]
    public async Task AFailedTry_IsNotRepeatedBeforeItsTime()
    {
        NewRow();
        _google.Throw = new GoogleCalendarException("down");
        await _service.SyncDueAsync(CancellationToken.None);

        _time.Now = _time.Now.AddSeconds(30);
        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Single(_google.Calls);
    }

    [Fact]
    public async Task AMeetingThatIsOver_IsNotTriedAgain()
    {
        var row = NewRow();
        _google.Throw = new GoogleCalendarException("down");
        _time.Now = new DateTimeOffset(row.EndsAtUtc.AddMinutes(1), TimeSpan.Zero);

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Failed, row.Status);
        Assert.Equal(MeetingSyncOutcome.Failed, Assert.IsType<MeetingSynced>(Assert.Single(_events.Published)).Outcome);
    }

    [Fact]
    public async Task WhenGoogleRefusesThePermission_TheRowAndTheConnectionWaitForAReconnect_WithoutRetrying()
    {
        var row = NewRow();
        _google.Throw = new GoogleAccessRevokedException("revoked");

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(new MeetingSyncRun(0, 0, 1), run);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, row.Status);
        Assert.Equal(GoogleConnectionStatus.NeedsReconnect, _connection.Status);
        Assert.Equal(MeetingSyncOutcome.NeedsReconnect, Assert.IsType<MeetingSynced>(Assert.Single(_events.Published)).Outcome);

        // Nothing is tried for this organiser until they connect again.
        var another = NewRow();
        _google.Throw = null;
        await _service.SyncDueAsync(CancellationToken.None);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, another.Status);
        Assert.Single(_google.Calls);
    }

    [Fact]
    public async Task WithoutAConnection_TheMeetingWaitsForOne()
    {
        var row = NewRow();
        _connections.Items.Clear();

        var run = await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(1, run.NeedReconnect);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, row.Status);
        Assert.Empty(_google.Calls);
    }

    [Fact]
    public async Task ASavedTokenThatCannotBeOpened_IsTreatedLikeARevokedOne()
    {
        var row = NewRow();
        _connections.Items.Clear();
        _connections.Items.Add(GoogleConnection.Connect(_organizer, "ana@gmail.example", "not-ours", Now()));

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.NeedsReconnect, row.Status);
        Assert.Equal(GoogleConnectionStatus.NeedsReconnect, _connections.Items[0].Status);
        Assert.Empty(_google.Calls);
    }

    [Fact]
    public async Task ANewerVersionThatArrivesWhileGoogleAnswers_IsNotMarkedDone_ButTheEventIsKept_AndNothingIsSent()
    {
        var row = NewRow();
        _google.DuringCall = () => Change(row, title: "Changed meanwhile");

        await _service.SyncDueAsync(CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Pending, row.Status);
        Assert.Equal(row.EventId, row.GoogleEventId);
        Assert.Equal(Now(), row.NextSyncAtUtc);
        Assert.Empty(_events.Published);

        _google.DuringCall = null;
        await _service.SyncDueAsync(CancellationToken.None);

        var second = _google.Calls[1];
        Assert.True(second.Exists);
        Assert.Equal("Changed meanwhile", second.Details!.Title);
        Assert.Equal(MeetingSyncStatus.Synced, row.Status);
        Assert.Equal(2, Assert.IsType<MeetingSynced>(Assert.Single(_events.Published)).Version);
    }

    [Fact]
    public async Task OneRowFailing_NeverStopsTheOthers()
    {
        var first = NewRow(title: "First");
        var second = NewRow(title: "Second");
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
        Assert.Equal(MeetingSyncStatus.Pending, first.Status);
        Assert.Equal(MeetingSyncStatus.Synced, second.Status);
    }

    private DateTime Now() => _time.Now.UtcDateTime;

    private MeetingSync NewRow(string title = "Kick-off", string? description = null)
    {
        var start = Now().AddDays(1);
        var row = MeetingSync.Create(Guid.NewGuid());
        row.Apply(1, _organizer, title, description, start, start.AddHours(1), "contact@acme.com", false, Now());
        _syncs.Items.Add(row);

        return row;
    }

    // What the next MeetingChanged of the meeting does to the row.
    private void Change(MeetingSync row, string? title = null, TimeSpan? moveBy = null, bool cancelled = false) =>
        row.Apply(
            row.Version + 1,
            row.OrganizerUserId,
            title ?? row.Title,
            row.Description,
            row.StartsAtUtc + (moveBy ?? TimeSpan.Zero),
            row.EndsAtUtc + (moveBy ?? TimeSpan.Zero),
            row.ClientEmail,
            cancelled,
            Now());
}
