using System.Net;
using System.Net.Http.Json;
using Partnr.Contracts.Events;
using Partnr.Core.Application.DTOs.Meetings;
using Partnr.Core.Application.Exceptions;
using Partnr.Core.Application.Interfaces.Services;
using Partnr.Core.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.Core.IntegrationTests.ClientView;

// What people see of a meeting's Google event. The sending itself is the Integrations service's: here its answers are played by hand,
// through the service the event handlers call (the whole trip over a broker is in Events/MeetingsOverTheBrokerTests).
public sealed partial class ClientProjectsTests
{
    [Fact]
    public async Task TheStateOfTheGoogleEvent_AndTheMeetLink_FollowTheAnswersOfTheIntegrationsService()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");

        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);
        Assert.Equal(("Pending", null), (meeting.SyncStatus, meeting.MeetUrl));
        Assert.Null((await ClientMeetings(elena, studio))[0].MeetUrl);

        await Answer(new MeetingSynced(meeting.Id, 1, MeetingSyncOutcome.Synced, "https://meet.google.com/abc-defg-hij"));

        var sent = await GetMeeting(studio, meeting.Id);
        Assert.Equal(("Synced", "https://meet.google.com/abc-defg-hij"), (sent.SyncStatus, sent.MeetUrl));
        Assert.Equal(sent.MeetUrl, (await ClientMeetings(elena, studio))[0].MeetUrl);

        var start = DateTimeOffset.UtcNow.AddDays(2);
        await studio.Http.PutAsJsonAsync($"{MeetingsUrl(studio)}/{meeting.Id}", new { title = "Moved", startsAt = start, endsAt = start.AddHours(1) });
        Assert.Equal("Pending", (await GetMeeting(studio, meeting.Id)).SyncStatus);

        // An answer about the first version arrives late: it says nothing about the meeting as it is now.
        await Answer(new MeetingSynced(meeting.Id, 1, MeetingSyncOutcome.Failed, null));
        Assert.Equal("Pending", (await GetMeeting(studio, meeting.Id)).SyncStatus);

        await Answer(new MeetingSynced(meeting.Id, 2, MeetingSyncOutcome.Synced, null));
        Assert.Equal(("Synced", "https://meet.google.com/abc-defg-hij"), ((await GetMeeting(studio, meeting.Id)).SyncStatus, (await GetMeeting(studio, meeting.Id)).MeetUrl));

        await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/cancel", null);
        await Answer(new MeetingSynced(meeting.Id, 3, MeetingSyncOutcome.Synced, "https://meet.google.com/abc-defg-hij"));

        Assert.Equal("Synced", (await GetMeeting(studio, meeting.Id)).SyncStatus);
        var seenByClient = (await ClientMeetings(elena, studio))[0];
        Assert.Equal(("Cancelled", null), (seenByClient.Status, seenByClient.MeetUrl));
    }

    [Fact]
    public async Task WhenIntegrationsGivesUp_AMemberCanTryAgain_OnceOnly()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);
        Assert.Equal(HttpStatusCode.Conflict, (await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/retry-sync", null)).StatusCode);

        await Answer(new MeetingSynced(meeting.Id, 1, MeetingSyncOutcome.Failed, null));
        Assert.Equal("Failed", (await GetMeeting(studio, meeting.Id)).SyncStatus);

        var retried = await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/retry-sync", null);
        Assert.Equal(HttpStatusCode.OK, retried.StatusCode);
        Assert.Equal("Pending", (await retried.Content.ReadFromJsonAsync<MeetingResponse>())!.SyncStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/retry-sync", null)).StatusCode);
    }

    [Fact]
    public async Task WhenTheGooglePermissionIsGone_TheMeetingAsksToReconnect_AndNoNewMeetingCanBeMade()
    {
        var studio = await Studio();
        var me = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);

        await Answer(new MeetingSynced(meeting.Id, 1, MeetingSyncOutcome.NeedsReconnect, null));
        factory.Google.Disconnect(me);

        Assert.Equal("NeedsReconnect", (await GetMeeting(studio, meeting.Id)).SyncStatus);
        Assert.Equal(HttpStatusCode.Conflict, (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), NewMeeting(elena))).StatusCode);
    }

    [Fact]
    public async Task WhenIntegrationsCannotBeReached_ACreateIsServiceUnavailable_ButAnUpdateStillWorks()
    {
        var studio = await Studio();
        var me = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var meeting = await Schedule(studio, elena, "Kick-off", null, days: 1);
        factory.Google.Unreachable(me);

        var refused = await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), NewMeeting(elena));
        var start = DateTimeOffset.UtcNow.AddDays(3);
        var updated = await studio.Http.PutAsJsonAsync($"{MeetingsUrl(studio)}/{meeting.Id}", new { title = "Fixed", startsAt = start, endsAt = start.AddHours(1) });

        Assert.Equal(HttpStatusCode.ServiceUnavailable, refused.StatusCode);
        Assert.Equal(HttpStatusCode.OK, updated.StatusCode);
    }

    [Fact]
    public async Task DisconnectingGoogle_LeavesWhatWasSent_ParksWhatWasNot_AndReconnectingPutsItBack()
    {
        var studio = await Studio();
        var me = await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var sent = await Schedule(studio, elena, "Sent", null, days: 1);
        await Answer(new MeetingSynced(sent.Id, 1, MeetingSyncOutcome.Synced, "https://meet.google.com/abc"));
        var waiting = await Schedule(studio, elena, "Waiting", null, days: 2);

        await Scoped(results => results.ParkAsync(me, CancellationToken.None));

        Assert.Equal("Synced", (await GetMeeting(studio, sent.Id)).SyncStatus);
        Assert.Equal("NeedsReconnect", (await GetMeeting(studio, waiting.Id)).SyncStatus);

        await Scoped(results => results.ResumeAsync(me, CancellationToken.None));

        Assert.Equal("Pending", (await GetMeeting(studio, waiting.Id)).SyncStatus);
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

    // What the handler of the event does when the answer of the Integrations service arrives.
    private Task Answer(MeetingSynced answer) => Scoped(results => results.ApplyAsync(answer, CancellationToken.None));

    private async Task Scoped(Func<IMeetingSyncResultService, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<IMeetingSyncResultService>());
    }

    private static async Task<MeetingResponse> GetMeeting(StudioSession studio, Guid id) =>
        (await studio.Http.GetFromJsonAsync<MeetingResponse>($"{MeetingsUrl(studio)}/{id}"))!;

    private static async Task<ClientMeetingResponse[]> ClientMeetings(ClientSession client, StudioSession studio) =>
        (await client.Http.GetFromJsonAsync<ClientMeetingResponse[]>(ClientMeetingsUrl(studio)))!;
}
