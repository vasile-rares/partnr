using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Domain.Enums;
using Partnr.Workspaces.Domain.Exceptions;

namespace Partnr.Workspaces.UnitTests.Domain;

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

        Assert.Equal((MeetingSyncStatus.Pending, 1, (string?)null), (meeting.SyncStatus, meeting.Version, meeting.MeetUrl));
    }

    [Fact]
    public void EveryChange_CountsAVersion_AndWaitsToBeSentAgain()
    {
        var meeting = NewMeeting();
        meeting.ApplySyncResult(1, MeetingSyncStatus.Synced, "https://meet.google.com/abc");

        meeting.Update(null, "Renamed", null, Start, Start.AddHours(1), Now.AddMinutes(1));

        Assert.Equal((MeetingSyncStatus.Pending, 2), (meeting.SyncStatus, meeting.Version));

        meeting.ApplySyncResult(2, MeetingSyncStatus.Synced, null);
        meeting.Cancel(Now.AddMinutes(3));

        Assert.Equal((MeetingSyncStatus.Pending, 3, "https://meet.google.com/abc"), (meeting.SyncStatus, meeting.Version, meeting.MeetUrl));
    }

    [Fact]
    public void AnAnswerForTheCurrentVersion_IsKept_AndALaterOneWithoutTheLinkDoesNotLoseIt()
    {
        var meeting = NewMeeting();

        Assert.True(meeting.ApplySyncResult(1, MeetingSyncStatus.Synced, "https://meet.google.com/abc"));
        Assert.True(meeting.ApplySyncResult(1, MeetingSyncStatus.Synced, null));

        Assert.Equal((MeetingSyncStatus.Synced, "https://meet.google.com/abc"), (meeting.SyncStatus, meeting.MeetUrl));
    }

    [Theory]
    [InlineData(MeetingSyncStatus.Synced)]
    [InlineData(MeetingSyncStatus.Failed)]
    [InlineData(MeetingSyncStatus.NeedsReconnect)]
    public void AnAnswerAboutAnOlderVersion_SaysNothingAboutTheMeetingAsItIsNow(MeetingSyncStatus answered)
    {
        var meeting = NewMeeting();
        meeting.Update(null, "Changed while sending", null, Start, Start.AddHours(1), Now);

        var applied = meeting.ApplySyncResult(1, answered, "https://meet.google.com/abc");

        Assert.False(applied);
        Assert.Equal((MeetingSyncStatus.Pending, (string?)null), (meeting.SyncStatus, meeting.MeetUrl));
    }

    [Fact]
    public void RetrySync_OnlyAfterFailing_AndNeverOnceItIsOver()
    {
        var meeting = NewMeeting();
        Assert.Throws<DomainException>(() => meeting.RetrySync(Now));

        meeting.ApplySyncResult(1, MeetingSyncStatus.Failed, null);
        meeting.RetrySync(Now.AddHours(1));
        Assert.Equal(MeetingSyncStatus.Pending, meeting.SyncStatus);

        meeting.ApplySyncResult(1, MeetingSyncStatus.Failed, null);
        Assert.Throws<DomainException>(() => meeting.RetrySync(Start.AddHours(2)));
    }

    [Fact]
    public void NeedsReconnect_OnlyParksWhatWasWaiting_AndResumeOnlyWakesThose()
    {
        var waiting = NewMeeting();
        var inStep = NewMeeting();
        inStep.ApplySyncResult(1, MeetingSyncStatus.Synced, null);

        waiting.MarkNeedsReconnect();
        inStep.MarkNeedsReconnect();

        Assert.Equal(MeetingSyncStatus.NeedsReconnect, waiting.SyncStatus);
        Assert.Equal(MeetingSyncStatus.Synced, inStep.SyncStatus);

        waiting.ResumeSync(Now.AddHours(1));
        inStep.ResumeSync(Now.AddHours(1));

        Assert.Equal(MeetingSyncStatus.Pending, waiting.SyncStatus);
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
