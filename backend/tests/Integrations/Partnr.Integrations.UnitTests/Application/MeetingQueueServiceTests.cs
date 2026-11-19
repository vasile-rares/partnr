using Partnr.Contracts.Events;
using Partnr.Integrations.Application.Services;
using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.UnitTests.Application;

public class MeetingQueueServiceTests
{
    private readonly FakeMeetingSyncRepository _syncs = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly MeetingQueueService _service;

    public MeetingQueueServiceTests()
    {
        _service = new MeetingQueueService(_syncs, _unitOfWork, _time);
    }

    [Fact]
    public async Task ANewMeeting_IsQueuedAtOnce_WithEverythingTheCalendarNeeds()
    {
        var change = Change(version: 1);

        await _service.ApplyAsync(change, CancellationToken.None);

        var row = Assert.Single(_syncs.Items);
        Assert.Equal(change.MeetingId, row.Id);
        Assert.Equal(("Kick-off", "contact@acme.com", 1), (row.Title, row.ClientEmail, row.Version));
        Assert.Equal(MeetingSyncStatus.Pending, row.Status);
        Assert.Equal(_time.Now.UtcDateTime, row.NextSyncAtUtc);
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task ANewerVersion_ReplacesTheContent_AndQueuesItAgain()
    {
        var first = Change(version: 1);
        await _service.ApplyAsync(first, CancellationToken.None);
        _syncs.Items.Single().RecordSynced(1, "event", "https://meet.google.com/x", _time.Now.UtcDateTime);

        await _service.ApplyAsync(first with { Version = 2, Title = "Moved" }, CancellationToken.None);

        var row = Assert.Single(_syncs.Items);
        Assert.Equal(("Moved", 2, MeetingSyncStatus.Pending), (row.Title, row.Version, row.Status));
        Assert.Equal("event", row.GoogleEventId);
    }

    [Theory]
    [InlineData(2)]
    [InlineData(3)]
    public async Task AnEventThatIsNotNewer_ARepeatOrOneThatArrivedLate_ChangesNothing(int delivered)
    {
        var first = Change(version: 3);
        await _service.ApplyAsync(first, CancellationToken.None);
        _syncs.Items.Single().RecordSynced(3, "event", null, _time.Now.UtcDateTime);
        var saved = _unitOfWork.SaveCount;

        await _service.ApplyAsync(first with { Version = delivered, Title = "Old" }, CancellationToken.None);

        var row = Assert.Single(_syncs.Items);
        Assert.Equal(("Kick-off", MeetingSyncStatus.Synced), (row.Title, row.Status));
        Assert.Equal(saved, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Retry_QueuesAMeetingThatGaveUp()
    {
        var change = Change(version: 1);
        await _service.ApplyAsync(change, CancellationToken.None);
        var row = _syncs.Items.Single();
        row.RecordSyncFailure(1, _time.Now.UtcDateTime.AddMinutes(1));
        for (var i = 0; i < MeetingSync.MaxSyncAttempts; i++)
        {
            row.RecordSyncFailure(1, _time.Now.UtcDateTime);
        }

        Assert.Equal(MeetingSyncStatus.Failed, row.Status);

        await _service.RetryAsync(change.MeetingId, CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Pending, row.Status);
        Assert.Equal(0, row.SyncAttempts);
    }

    [Fact]
    public async Task Retry_OfAnythingElse_DoesNothing()
    {
        var change = Change(version: 1);
        await _service.ApplyAsync(change, CancellationToken.None);
        var saved = _unitOfWork.SaveCount;

        await _service.RetryAsync(change.MeetingId, CancellationToken.None);
        await _service.RetryAsync(Guid.NewGuid(), CancellationToken.None);

        Assert.Equal(saved, _unitOfWork.SaveCount);
    }

    private MeetingChanged Change(int version)
    {
        var start = _time.Now.UtcDateTime.AddDays(1);

        return new MeetingChanged(Guid.NewGuid(), version, Guid.NewGuid(), "Kick-off", null, start, start.AddHours(1), "contact@acme.com", false);
    }
}
