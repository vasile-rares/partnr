using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.UnitTests.Domain;

public class MeetingSyncTests
{
    private static readonly DateTime Now = new(2026, 10, 14, 9, 0, 0, DateTimeKind.Utc);
    private static readonly Guid Organizer = Guid.NewGuid();

    [Fact]
    public void ANewRow_TakesTheFirstVersion_AndIsQueuedAtOnce()
    {
        var row = Row();

        Assert.Equal(1, row.Version);
        Assert.Equal(MeetingSyncStatus.Pending, row.Status);
        Assert.Equal(Now, row.NextSyncAtUtc);
        Assert.False(row.IsSettled);
    }

    [Fact]
    public void TheEventInGoogleIsNamedAfterTheMeeting()
    {
        var id = Guid.NewGuid();

        Assert.Equal(id.ToString("N"), MeetingSync.Create(id).EventId);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(0)]
    public void AnEventThatIsNotNewer_IsRefused_AndChangesNothing(int version)
    {
        var row = Row();

        var applied = row.Apply(version, Organizer, "Other", null, row.StartsAtUtc, row.EndsAtUtc, "other@acme.com", true, Now.AddHours(1));

        Assert.False(applied);
        Assert.Equal(("Kick-off", "contact@acme.com", false), (row.Title, row.ClientEmail, row.IsCancelled));
    }

    [Fact]
    public void ANewerVersion_QueuesAgain_AndStartsTheCountOfTriesOver()
    {
        var row = Row();
        row.RecordSyncFailure(1, Now);

        row.Apply(2, Organizer, "Moved", " notes ", row.StartsAtUtc, row.EndsAtUtc, "contact@acme.com", false, Now.AddMinutes(3));

        Assert.Equal(("Moved", "notes", MeetingSyncStatus.Pending, 0), (row.Title, row.Description, row.Status, row.SyncAttempts));
        Assert.Equal(Now.AddMinutes(3), row.NextSyncAtUtc);
    }

    [Fact]
    public void WhatWasSentForAnOlderVersion_KeepsTheEventButGoesRoundAgain()
    {
        var row = Row();
        row.Apply(2, Organizer, "Moved", null, row.StartsAtUtc, row.EndsAtUtc, "contact@acme.com", false, Now);

        row.RecordSynced(1, "event", "https://meet.google.com/x", Now.AddMinutes(1));

        Assert.Equal(("event", "https://meet.google.com/x", MeetingSyncStatus.Pending), (row.GoogleEventId, row.MeetUrl, row.Status));
        Assert.Equal(Now.AddMinutes(1), row.NextSyncAtUtc);
    }

    [Fact]
    public void WhatWasSentForTheCurrentVersion_IsSettled()
    {
        var row = Row();

        row.RecordSynced(1, "event", null, Now);

        Assert.Equal(MeetingSyncStatus.Synced, row.Status);
        Assert.Null(row.NextSyncAtUtc);
        Assert.True(row.IsSettled);
    }

    [Fact]
    public void Retry_WorksOnlyForAGivenUpMeetingThatHasNotEnded()
    {
        var row = Row();
        Assert.False(row.Retry(Now));

        GiveUp(row);
        Assert.False(row.Retry(row.EndsAtUtc.AddMinutes(1)));
        Assert.True(row.Retry(Now));
        Assert.Equal(MeetingSyncStatus.Pending, row.Status);
    }

    [Fact]
    public void Resume_WorksOnlyForARowThatWaitedForAReconnect_AndNotAfterTheMeetingEnded()
    {
        var row = Row();
        Assert.False(row.Resume(Now));

        row.MarkNeedsReconnect();
        Assert.False(row.Resume(row.EndsAtUtc.AddMinutes(1)));
        Assert.True(row.Resume(Now));
        Assert.Equal(MeetingSyncStatus.Pending, row.Status);
    }

    [Fact]
    public void MarkNeedsReconnect_LeavesAnInStepRowAlone()
    {
        var row = Row();
        row.RecordSynced(1, "event", null, Now);

        Assert.False(row.MarkNeedsReconnect());
        Assert.Equal(MeetingSyncStatus.Synced, row.Status);
    }

    private static MeetingSync Row()
    {
        var row = MeetingSync.Create(Guid.NewGuid());
        row.Apply(1, Organizer, "Kick-off", null, Now.AddDays(1), Now.AddDays(1).AddHours(1), "contact@acme.com", false, Now);

        return row;
    }

    private static void GiveUp(MeetingSync row)
    {
        for (var i = 0; i < MeetingSync.MaxSyncAttempts; i++)
        {
            row.RecordSyncFailure(row.Version, Now);
        }
    }
}
