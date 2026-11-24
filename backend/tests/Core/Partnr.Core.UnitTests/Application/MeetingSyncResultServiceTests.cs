using Partnr.Contracts.Events;
using Partnr.Core.Application.Services;
using Partnr.Core.Domain.Entities;
using Partnr.Core.Domain.Enums;

namespace Partnr.Core.UnitTests.Application;

public class MeetingSyncResultServiceTests
{
    private readonly Guid _organizer = Guid.NewGuid();
    private readonly FakeMeetingRepository _meetings = new();
    private readonly FakeUnitOfWork _unitOfWork = new();
    private readonly FakeTimeProvider _time = new();
    private readonly MeetingSyncResultService _service;

    public MeetingSyncResultServiceTests()
    {
        _service = new MeetingSyncResultService(_meetings, _unitOfWork, _time);
    }

    [Theory]
    [InlineData(MeetingSyncOutcome.Synced, MeetingSyncStatus.Synced)]
    [InlineData(MeetingSyncOutcome.Failed, MeetingSyncStatus.Failed)]
    [InlineData(MeetingSyncOutcome.NeedsReconnect, MeetingSyncStatus.NeedsReconnect)]
    public async Task AnAnswerForTheCurrentVersion_IsShown_WithTheMeetLink(MeetingSyncOutcome outcome, MeetingSyncStatus expected)
    {
        var meeting = NewMeeting();

        await _service.ApplyAsync(new MeetingSynced(meeting.Id, 1, outcome, "https://meet.google.com/abc"), CancellationToken.None);

        Assert.Equal((expected, "https://meet.google.com/abc"), (meeting.SyncStatus, meeting.MeetUrl));
        Assert.Equal(1, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAnswerAboutAnOlderVersion_ChangesNothing_AndIsNotSaved()
    {
        var meeting = NewMeeting();
        meeting.Update(null, "Moved", null, meeting.StartsAtUtc, meeting.EndsAtUtc, Now());

        await _service.ApplyAsync(new MeetingSynced(meeting.Id, 1, MeetingSyncOutcome.Synced, "https://meet.google.com/abc"), CancellationToken.None);

        Assert.Equal((MeetingSyncStatus.Pending, (string?)null), (meeting.SyncStatus, meeting.MeetUrl));
        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task AnAnswerAboutAMeetingThatIsNotHere_IsIgnored()
    {
        await _service.ApplyAsync(new MeetingSynced(Guid.NewGuid(), 1, MeetingSyncOutcome.Synced, null), CancellationToken.None);

        Assert.Equal(0, _unitOfWork.SaveCount);
    }

    [Fact]
    public async Task Park_WaitsForAReconnectOnlyWhatWasStillWaiting_OfThatOrganiser()
    {
        var waiting = NewMeeting();
        var inStep = NewMeeting();
        inStep.ApplySyncResult(1, MeetingSyncStatus.Synced, null);
        var someoneElses = NewMeeting(Guid.NewGuid());

        await _service.ParkAsync(_organizer, CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.NeedsReconnect, waiting.SyncStatus);
        Assert.Equal(MeetingSyncStatus.Synced, inStep.SyncStatus);
        Assert.Equal(MeetingSyncStatus.Pending, someoneElses.SyncStatus);
    }

    [Fact]
    public async Task Resume_PutsWhatWaitedBackInTheQueue_ButNotAMeetingThatEnded()
    {
        var waiting = NewMeeting();
        waiting.MarkNeedsReconnect();
        var over = NewMeeting(startsInDays: -1);
        over.MarkNeedsReconnect();
        var someoneElses = NewMeeting(Guid.NewGuid());
        someoneElses.MarkNeedsReconnect();

        await _service.ResumeAsync(_organizer, CancellationToken.None);

        Assert.Equal(MeetingSyncStatus.Pending, waiting.SyncStatus);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, over.SyncStatus);
        Assert.Equal(MeetingSyncStatus.NeedsReconnect, someoneElses.SyncStatus);
    }

    private DateTime Now() => _time.Now.UtcDateTime;

    private Meeting NewMeeting(Guid? organizer = null, int startsInDays = 1)
    {
        var start = Now().AddDays(startsInDays);
        var scheduledAt = startsInDays >= 0 ? Now() : start.AddDays(-1);
        var meeting = Meeting.Schedule(Guid.NewGuid(), Guid.NewGuid(), organizer ?? _organizer, null, "Kick-off", null, start, start.AddHours(1), scheduledAt);
        _meetings.Items.Add(meeting);

        return meeting;
    }
}
