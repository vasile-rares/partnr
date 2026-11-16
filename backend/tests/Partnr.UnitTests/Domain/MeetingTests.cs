using Partnr.Domain.Entities;
using Partnr.Domain.Enums;
using Partnr.Domain.Exceptions;

namespace Partnr.UnitTests.Domain;

public class MeetingTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly DateTime Start = Now.AddDays(1);

    private static Meeting NewMeeting(string title = "Kick-off", DateTime? start = null, DateTime? end = null, Guid? projectId = null) =>
        Meeting.Schedule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), projectId, title, null, start ?? Start, end ?? Start.AddHours(1), Now);

    [Fact]
    public void Schedule_StartsScheduled_WithTrimmedTextAndNoEmptyNote()
    {
        var meeting = Meeting.Schedule(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, "  Kick-off ", "   ", Start, Start.AddHours(1), Now);

        Assert.Equal(MeetingStatus.Scheduled, meeting.Status);
        Assert.Equal("Kick-off", meeting.Title);
        Assert.Null(meeting.Description);
        Assert.Null(meeting.CancelledAtUtc);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Schedule_WithoutATitle_Throws(string title) => Assert.Throws<DomainException>(() => NewMeeting(title));

    [Fact]
    public void Schedule_ForAnythingAlreadyPast_Throws()
    {
        Assert.Throws<DomainException>(() => NewMeeting(start: Now.AddMinutes(-1), end: Now.AddMinutes(30)));
        Assert.Throws<DomainException>(() => NewMeeting(start: Now, end: Now.AddHours(1)));
    }

    [Theory]
    [InlineData(4)]
    [InlineData(0)]
    [InlineData(-30)]
    [InlineData(481)]
    public void Schedule_OutsideFiveMinutesToEightHours_Throws(int minutes) =>
        Assert.Throws<DomainException>(() => NewMeeting(end: Start.AddMinutes(minutes)));

    [Theory]
    [InlineData(5)]
    [InlineData(480)]
    public void Schedule_AtTheLimitsOfTheLength_Works(int minutes) => NewMeeting(end: Start.AddMinutes(minutes));

    [Fact]
    public void Schedule_WithEmptyIds_Throws()
    {
        Assert.Throws<DomainException>(() => Meeting.Schedule(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), null, "x", null, Start, Start.AddHours(1), Now));
        Assert.Throws<DomainException>(() => Meeting.Schedule(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), null, "x", null, Start, Start.AddHours(1), Now));
        Assert.Throws<DomainException>(() => Meeting.Schedule(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, null, "x", null, Start, Start.AddHours(1), Now));
        Assert.Throws<DomainException>(() => NewMeeting(projectId: Guid.Empty));
    }

    [Fact]
    public void Update_ReplacesEverything_ButTheClient()
    {
        var meeting = NewMeeting();
        var clientId = meeting.ClientId;
        var project = Guid.NewGuid();

        meeting.Update(project, "Review", "Bring the mockups", Start.AddDays(1), Start.AddDays(1).AddMinutes(45), Now);

        Assert.Equal(project, meeting.ProjectId);
        Assert.Equal("Review", meeting.Title);
        Assert.Equal("Bring the mockups", meeting.Description);
        Assert.Equal(Start.AddDays(1), meeting.StartsAtUtc);
        Assert.Equal(clientId, meeting.ClientId);
    }

    [Fact]
    public void Update_MovingItToThePast_Throws_ButFixingTheTitleWhileItRunsWorks()
    {
        var meeting = NewMeeting();
        Assert.Throws<DomainException>(() => meeting.Update(null, "x", null, Now.AddMinutes(-5), Now.AddMinutes(25), Now));

        // Half an hour into it, with the same times: only the words change.
        meeting.Update(null, "Kick-off (fixed)", null, Start, Start.AddHours(1), Start.AddMinutes(30));

        Assert.Equal("Kick-off (fixed)", meeting.Title);
    }

    [Fact]
    public void Update_AfterItEnded_OrWhenCancelled_Throws()
    {
        var ended = NewMeeting();
        Assert.Throws<DomainException>(() => ended.Update(null, "x", null, Start, Start.AddHours(1), Start.AddHours(1)));

        var cancelled = NewMeeting();
        cancelled.Cancel(Now);
        Assert.Throws<DomainException>(() => cancelled.Update(null, "x", null, Start, Start.AddHours(1), Now));
    }

    [Fact]
    public void Cancel_KeepsTheMeetingAsHistory_AndCannotRepeat()
    {
        var meeting = NewMeeting();

        meeting.Cancel(Now);

        Assert.Equal(MeetingStatus.Cancelled, meeting.Status);
        Assert.Equal(Now, meeting.CancelledAtUtc);
        Assert.Throws<DomainException>(() => meeting.Cancel(Now.AddMinutes(1)));
    }

    [Fact]
    public void Cancel_AfterItEnded_Throws() =>
        Assert.Throws<DomainException>(() => NewMeeting().Cancel(Start.AddHours(2)));

    [Fact]
    public void ANewMeeting_WaitsToBeSent_AtVersionOne()
    {
        var meeting = NewMeeting();

        Assert.Equal((MeetingSyncStatus.Pending, 1, Now, 0), (meeting.SyncStatus, meeting.Version, meeting.NextSyncAtUtc, meeting.SyncAttempts));
        Assert.Null(meeting.GoogleEventId);
        Assert.Equal(meeting.Id.ToString("N"), meeting.EventId);
    }

    [Fact]
    public void EveryChange_CountsAVersion_AndQueuesTheMeetingAgain()
    {
        var meeting = NewMeeting();
        meeting.RecordSynced(1, "event", "https://meet.google.com/abc", Now);

        meeting.Update(null, "Renamed", null, Start, Start.AddHours(1), Now.AddMinutes(1));

        Assert.Equal((MeetingSyncStatus.Pending, 2, Now.AddMinutes(1)), (meeting.SyncStatus, meeting.Version, meeting.NextSyncAtUtc));

        meeting.RecordSynced(2, null, null, Now.AddMinutes(2));
        meeting.Cancel(Now.AddMinutes(3));

        Assert.Equal((MeetingSyncStatus.Pending, 3), (meeting.SyncStatus, meeting.Version));
        Assert.Equal("event", meeting.GoogleEventId);
    }

    [Fact]
    public void ASyncOfTheCurrentVersion_MarksItInStep_AndKeepsTheEventAndTheLinkLater()
    {
        var meeting = NewMeeting();

        meeting.RecordSynced(1, "event", "https://meet.google.com/abc", Now);
        meeting.RecordSynced(1, "event", null, Now); // a later answer without the link does not lose it

        Assert.Equal((MeetingSyncStatus.Synced, "event", "https://meet.google.com/abc"), (meeting.SyncStatus, meeting.GoogleEventId, meeting.MeetUrl));
        Assert.Null(meeting.NextSyncAtUtc);
    }

    [Fact]
    public void ASyncOfAnOlderVersion_KeepsTheEvent_ButGoesRoundAgain()
    {
        var meeting = NewMeeting();
        meeting.Update(null, "Changed while sending", null, Start, Start.AddHours(1), Now);

        meeting.RecordSynced(1, "event", "https://meet.google.com/abc", Now.AddSeconds(5));

        Assert.Equal((MeetingSyncStatus.Pending, "event", Now.AddSeconds(5)), (meeting.SyncStatus, meeting.GoogleEventId, meeting.NextSyncAtUtc));
    }

    [Fact]
    public void Failures_BackOffThenGiveUp()
    {
        var meeting = NewMeeting();
        var waits = new List<TimeSpan>();

        for (var i = 0; i < 4; i++)
        {
            meeting.RecordSyncFailure(1, Now);
            waits.Add(meeting.NextSyncAtUtc!.Value - Now);
        }

        Assert.Equal([TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)], waits);

        meeting.RecordSyncFailure(1, Now);

        Assert.Equal((MeetingSyncStatus.Failed, 5), (meeting.SyncStatus, meeting.SyncAttempts));
        Assert.Null(meeting.NextSyncAtUtc);
    }

    [Fact]
    public void AFailureOfAnOlderVersion_DoesNotCount_ItGoesRoundAgain()
    {
        var meeting = NewMeeting();
        meeting.Update(null, "Changed", null, Start, Start.AddHours(1), Now);

        meeting.RecordSyncFailure(1, Now.AddSeconds(5));

        Assert.Equal((MeetingSyncStatus.Pending, 0), (meeting.SyncStatus, meeting.SyncAttempts));
    }

    [Fact]
    public void AMeetingThatIsOver_GivesUpAtTheFirstFailure()
    {
        var meeting = NewMeeting();

        meeting.RecordSyncFailure(1, Start.AddHours(2));

        Assert.Equal(MeetingSyncStatus.Failed, meeting.SyncStatus);
    }

    [Fact]
    public void RetrySync_OnlyAfterFailing_AndNeverOnceItIsOver()
    {
        var meeting = NewMeeting();
        Assert.Throws<DomainException>(() => meeting.RetrySync(Now));

        for (var i = 0; i < 5; i++)
        {
            meeting.RecordSyncFailure(1, Now);
        }

        meeting.RetrySync(Now.AddHours(1));
        Assert.Equal((MeetingSyncStatus.Pending, 0, Now.AddHours(1)), (meeting.SyncStatus, meeting.SyncAttempts, meeting.NextSyncAtUtc));

        meeting.RecordSyncFailure(1, Start.AddHours(2));
        Assert.Throws<DomainException>(() => meeting.RetrySync(Start.AddHours(2)));
    }

    [Fact]
    public void NeedsReconnect_OnlyParksWhatWasWaiting_AndResumeOnlyWakesThose()
    {
        var waiting = NewMeeting();
        var inStep = NewMeeting();
        inStep.RecordSynced(1, "event", null, Now);

        waiting.MarkNeedsReconnect();
        inStep.MarkNeedsReconnect();

        Assert.Equal((MeetingSyncStatus.NeedsReconnect, (DateTime?)null), (waiting.SyncStatus, waiting.NextSyncAtUtc));
        Assert.Equal(MeetingSyncStatus.Synced, inStep.SyncStatus);

        waiting.ResumeSync(Now.AddHours(1));
        inStep.ResumeSync(Now.AddHours(1));

        Assert.Equal((MeetingSyncStatus.Pending, Now.AddHours(1)), (waiting.SyncStatus, waiting.NextSyncAtUtc));
        Assert.Equal(MeetingSyncStatus.Synced, inStep.SyncStatus);
    }

    [Fact]
    public void ResumeSync_DoesNothingOnceTheMeetingIsOver()
    {
        var meeting = NewMeeting();
        meeting.MarkNeedsReconnect();

        meeting.ResumeSync(Start.AddHours(2));

        Assert.Equal(MeetingSyncStatus.NeedsReconnect, meeting.SyncStatus);
    }
}
