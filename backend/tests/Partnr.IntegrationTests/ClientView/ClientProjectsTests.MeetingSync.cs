using System.Net;
using System.Net.Http.Json;
using Partnr.Application.DTOs.Google;
using Partnr.Application.DTOs.Meetings;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Services;
using Partnr.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.IntegrationTests.ClientView;

// Sending meetings to Google. The background worker is off in these tests: they run the sync themselves, against the real database.
public sealed partial class ClientProjectsTests
{
    [Fact]
    public async Task AMeeting_IsSentToGoogle_ChangedThere_AndDeletedThere_AndTheClientSeesTheMeetLink()
    {
        var studio = await Studio();
        var token = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");

        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);
        Assert.Equal(("Pending", null), (meeting.SyncStatus, meeting.MeetUrl));
        Assert.Null((await ClientMeetings(elena, studio))[0].MeetUrl);

        await SyncAll();

        var sent = await GetMeeting(studio, meeting.Id);
        Assert.Equal("Synced", sent.SyncStatus);
        Assert.StartsWith("https://meet.google.com/", sent.MeetUrl);
        var create = Assert.Single(factory.Calendar.CallsFor(token));
        Assert.Equal(("upsert", false, elena.Email), (create.Operation, create.Exists, create.Details!.AttendeeEmail));
        Assert.Equal(sent.MeetUrl, (await ClientMeetings(elena, studio))[0].MeetUrl);

        var start = DateTimeOffset.UtcNow.AddDays(2);
        await studio.Http.PutAsJsonAsync($"{MeetingsUrl(studio)}/{meeting.Id}", new { title = "Moved", startsAt = start, endsAt = start.AddHours(1) });
        Assert.Equal("Pending", (await GetMeeting(studio, meeting.Id)).SyncStatus);
        await SyncAll();

        var update = factory.Calendar.CallsFor(token)[1];
        Assert.Equal(("upsert", true, "Moved"), (update.Operation, update.Exists, update.Details!.Title));
        Assert.Equal(meeting.Id.ToString("N"), update.EventId);

        await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/cancel", null);
        await SyncAll();

        var delete = factory.Calendar.CallsFor(token)[2];
        Assert.Equal(("delete", meeting.Id.ToString("N")), (delete.Operation, delete.EventId));
        Assert.Equal("Synced", (await GetMeeting(studio, meeting.Id)).SyncStatus);
        var seenByClient = (await ClientMeetings(elena, studio))[0];
        Assert.Equal(("Cancelled", null), (seenByClient.Status, seenByClient.MeetUrl));
    }

    [Fact]
    public async Task AMeetingCancelledBeforeItWasSent_NeverReachesGoogle()
    {
        var studio = await Studio();
        var token = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);
        await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/cancel", null);

        await SyncAll();

        Assert.Empty(factory.Calendar.CallsFor(token));
        Assert.Equal("Synced", (await GetMeeting(studio, meeting.Id)).SyncStatus);
    }

    [Fact]
    public async Task TwoWorkersAtOnce_NeverSendTheSameMeetingTwice()
    {
        var studio = await Studio();
        var token = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        await Schedule(studio, elena, "Kick-off", null, days: 1);
        factory.Calendar.Delays[token] = TimeSpan.FromMilliseconds(400);

        await Task.WhenAll(SyncAll(), SyncAll(), SyncAll());

        Assert.Single(factory.Calendar.CallsFor(token));
    }

    [Fact]
    public async Task WhenGoogleKeepsFailing_ItGivesUpAfterFiveTries_AndAMemberCanTryAgain()
    {
        var studio = await Studio();
        var token = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);
        factory.Calendar.Failures[token] = new GoogleCalendarException("Google is down");

        for (var attempt = 1; attempt <= 5; attempt++)
        {
            await SyncAll();
            var state = await GetMeeting(studio, meeting.Id);
            Assert.Equal(attempt < 5 ? "Pending" : "Failed", state.SyncStatus);
            await MakeDue(meeting.Id);
        }

        Assert.Equal(5, factory.Calendar.CallsFor(token).Count);
        await SyncAll();
        Assert.Equal(5, factory.Calendar.CallsFor(token).Count); // given up: nothing more is tried on its own

        factory.Calendar.Failures.TryRemove(token, out _);
        var retried = await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/retry-sync", null);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("Pending", (await retried.Content.ReadFromJsonAsync<MeetingResponse>())!.SyncStatus);
        await SyncAll();

        Assert.Equal("Synced", (await GetMeeting(studio, meeting.Id)).SyncStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/retry-sync", null)).StatusCode);
    }

    [Fact]
    public async Task WhenGoogleTakesThePermissionBack_NothingIsRetried_AndTheAccountAsksToReconnect()
    {
        var studio = await Studio();
        var token = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);
        factory.Calendar.Failures[token] = new GoogleAccessRevokedException("revoked");

        await SyncAll();

        Assert.Equal("NeedsReconnect", (await GetMeeting(studio, meeting.Id)).SyncStatus);
        Assert.Equal("NeedsReconnect", (await studio.Http.GetFromJsonAsync<GoogleConnectionResponse>("/api/integrations/google"))!.Status);
        await MakeDue(meeting.Id);
        await SyncAll();
        Assert.Single(factory.Calendar.CallsFor(token));

        // And no new meeting can be made until they connect again.
        Assert.Equal(HttpStatusCode.Conflict, (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), NewMeeting(elena))).StatusCode);
    }

    [Fact]
    public async Task DisconnectingGoogle_LeavesWhatWasSent_AndParksWhatWasNot()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var sent = await Schedule(studio, elena, "Sent", null, days: 1);
        await SyncAll();
        var waiting = await Schedule(studio, elena, "Waiting", null, days: 2);

        Assert.Equal(HttpStatusCode.NoContent, (await studio.Http.DeleteAsync("/api/integrations/google")).StatusCode);

        Assert.Equal("Synced", (await GetMeeting(studio, sent.Id)).SyncStatus);
        Assert.Equal("NeedsReconnect", (await GetMeeting(studio, waiting.Id)).SyncStatus);
    }

    [Fact]
    public async Task TwoSavesOfTheSameMeetingAtOnce_ThenTheSecondOneIsRefused_InsteadOfUndoingTheFirst()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);

        using var first = factory.Services.CreateScope();
        using var second = factory.Services.CreateScope();
        var firstDb = first.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var secondDb = second.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var a = await firstDb.Meetings.SingleAsync(m => m.Id == meeting.Id);
        var b = await secondDb.Meetings.SingleAsync(m => m.Id == meeting.Id);
        var now = DateTime.UtcNow;

        a.Update(null, "First", null, a.StartsAtUtc, a.EndsAtUtc, now);
        b.Update(null, "Second", null, b.StartsAtUtc, b.EndsAtUtc, now);
        await firstDb.SaveChangesAsync();

        await Assert.ThrowsAsync<ConflictException>(() => secondDb.SaveChangesAsync());
        Assert.Equal("First", (await GetMeeting(studio, meeting.Id)).Title);
    }

    // The worker takes a few meetings at a time, and meetings of earlier tests may still be waiting: go round until none is due.
    private async Task SyncAll()
    {
        for (var round = 0; round < 20; round++)
        {
            using var scope = factory.Services.CreateScope();
            var run = await scope.ServiceProvider.GetRequiredService<IMeetingSyncService>().SyncDueAsync(CancellationToken.None);
            if (run == new MeetingSyncRun(0, 0, 0))
            {
                return;
            }
        }
    }

    // Skips the wait between tries.
    private async Task MakeDue(Guid meetingId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        await db.Meetings.Where(m => m.Id == meetingId && m.NextSyncAtUtc != null)
            .ExecuteUpdateAsync(s => s.SetProperty(m => m.NextSyncAtUtc, DateTime.UtcNow.AddSeconds(-1)));
    }

    private static async Task<MeetingResponse> GetMeeting(StudioSession studio, Guid id) =>
        (await studio.Http.GetFromJsonAsync<MeetingResponse>($"{MeetingsUrl(studio)}/{id}"))!;

    private static async Task<ClientMeetingResponse[]> ClientMeetings(ClientSession client, StudioSession studio) =>
        (await client.Http.GetFromJsonAsync<ClientMeetingResponse[]>(ClientMeetingsUrl(studio)))!;
}
