using System.Net;
using System.Net.Http.Json;
using Partnr.Application.DTOs.Auth;
using Partnr.Application.DTOs.Meetings;
using Partnr.Application.Interfaces.Services;
using Partnr.Domain.Entities;
using Partnr.Infrastructure.Persistence;
using Microsoft.Extensions.DependencyInjection;

namespace Partnr.IntegrationTests.Portal;

// Meetings, from both doors: the firm's (/api/workspaces/...) and the client's (/api/portal/workspaces/...).
public sealed partial class PortalProjectsTests
{
    [Fact]
    public async Task AMeeting_NeedsTheOrganisersGoogleAccountToBeConnected()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");

        var refused = await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), NewMeeting(elena));
        Assert.Equal(HttpStatusCode.Conflict, refused.StatusCode);
        Assert.Empty((await studio.Http.GetFromJsonAsync<MeetingResponse[]>(MeetingsUrl(studio)))!);

        await ConnectGoogle(studio.Http);
        Assert.Equal(HttpStatusCode.Created, (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), NewMeeting(elena))).StatusCode);
    }

    [Fact]
    public async Task TheFirm_SchedulesChangesAndCancelsAMeeting_AndKeepsItInTheHistory()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");

        var created = await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), NewMeeting(elena, "Kick-off", description: "  Bring the brief  "));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var meeting = (await created.Content.ReadFromJsonAsync<MeetingResponse>())!;
        Assert.Equal(("Kick-off", "Bring the brief", "Scheduled", "Elena"), (meeting.Title, meeting.Description, meeting.Status, meeting.Client.DisplayName));
        Assert.Equal(meeting.Id, (await studio.Http.GetFromJsonAsync<MeetingResponse>($"{MeetingsUrl(studio)}/{meeting.Id}"))!.Id);

        var start = DateTimeOffset.UtcNow.AddDays(3);
        var changed = await studio.Http.PutAsJsonAsync($"{MeetingsUrl(studio)}/{meeting.Id}", new { title = "Moved", startsAt = start, endsAt = start.AddMinutes(30) });
        Assert.Equal(HttpStatusCode.OK, changed.StatusCode);
        var moved = (await changed.Content.ReadFromJsonAsync<MeetingResponse>())!;
        Assert.Equal(("Moved", null), (moved.Title, moved.Description));
        Assert.Equal(start.UtcDateTime, moved.StartsAtUtc, TimeSpan.FromMilliseconds(1));

        var cancelled = await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/cancel", null);
        Assert.Equal("Cancelled", (await cancelled.Content.ReadFromJsonAsync<MeetingResponse>())!.Status);
        Assert.Empty((await studio.Http.GetFromJsonAsync<MeetingResponse[]>(MeetingsUrl(studio)))!);
        Assert.Single((await studio.Http.GetFromJsonAsync<MeetingResponse[]>($"{MeetingsUrl(studio)}?includeCancelled=true"))!);

        // A cancelled meeting is history: nothing changes it again.
        Assert.Equal(HttpStatusCode.Conflict, (await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{meeting.Id}/cancel", null)).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await studio.Http.PutAsJsonAsync($"{MeetingsUrl(studio)}/{meeting.Id}", new { title = "Again", startsAt = start, endsAt = start.AddHours(1) })).StatusCode);
    }

    [Fact]
    public async Task AMeeting_IsRefusedWhenItsTimeOrItsProjectDoesNotMakeSense()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var other = await studio.AddClient("Other");
        var theirs = await studio.AddProject(other, "Theirs");
        var past = DateTimeOffset.UtcNow.AddHours(-2);
        var soon = DateTimeOffset.UtcNow.AddDays(1);

        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), new { clientId = elena.ClientId, title = "Past", startsAt = past, endsAt = past.AddHours(1) })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), new { clientId = elena.ClientId, title = "Backwards", startsAt = soon, endsAt = soon.AddHours(-1) })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), new { clientId = elena.ClientId, startsAt = soon, endsAt = soon.AddHours(1) })).StatusCode);
        Assert.Equal(
            HttpStatusCode.BadRequest,
            (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), new { clientId = elena.ClientId, title = "No end", startsAt = soon })).StatusCode);
        Assert.Equal(
            HttpStatusCode.Conflict,
            (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), new { clientId = elena.ClientId, projectId = theirs.Id, title = "Wrong project", startsAt = soon, endsAt = soon.AddHours(1) })).StatusCode);
        Assert.Equal(
            HttpStatusCode.NotFound,
            (await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), new { clientId = Guid.NewGuid(), title = "Nobody", startsAt = soon, endsAt = soon.AddHours(1) })).StatusCode);
        Assert.Empty((await studio.Http.GetFromJsonAsync<MeetingResponse[]>(MeetingsUrl(studio)))!);
    }

    [Fact]
    public async Task TheClient_SeesItsMeetings_WithTheProjectNamedOnlyWhenItCanOpenIt()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var website = await studio.AddProject(elena, "Website");
        await studio.SetStatus(website, "Active");
        var secret = await studio.AddProject(elena, "Secret plan"); // Planning: the client does not see it
        await Schedule(studio, elena, "About the website", website.Id, days: 1);
        await Schedule(studio, elena, "About the secret", secret.Id, days: 2);
        var cancelled = await Schedule(studio, elena, "Called off", null, days: 3);
        await studio.Http.PostAsync($"{MeetingsUrl(studio)}/{cancelled.Id}/cancel", null);

        var list = (await elena.Http.GetFromJsonAsync<PortalMeetingResponse[]>(PortalMeetingsUrl(studio)))!;

        Assert.Equal(["About the website", "About the secret", "Called off"], list.Select(m => m.Title));
        Assert.Equal("Website", list[0].Project?.Name);
        Assert.Null(list[1].Project);
        Assert.Equal("Cancelled", list[2].Status);
        Assert.DoesNotContain("Secret", await elena.Http.GetStringAsync(PortalMeetingsUrl(studio)));
    }

    [Fact]
    public async Task TheClient_NeverSeesAnotherClientsMeetings_AndOnlyTheFirmsOwnMembersCanManageThem()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        var other = await studio.AddClient("Other");
        await Schedule(studio, elena, "Elena's", null, days: 1);
        var theirs = await Schedule(studio, other, "Theirs", null, days: 1);

        Assert.Equal(["Elena's"], (await elena.Http.GetFromJsonAsync<PortalMeetingResponse[]>(PortalMeetingsUrl(studio)))!.Select(m => m.Title));

        // The client's door only reads, and the firm's door is not theirs.
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(MeetingsUrl(studio))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.PostAsync($"{MeetingsUrl(studio)}/{theirs.Id}/cancel", null)).StatusCode);
    }

    [Fact]
    public async Task APortalDoor_IsClosedToTheFirmsOwnMembers_ToOtherFirms_AndToAnonymousVisitors()
    {
        var studio = await Studio();
        var elena = await studio.AddClient("Elena");
        var otherFirm = await Studio();

        Assert.Equal(HttpStatusCode.NotFound, (await studio.Http.GetAsync(PortalMeetingsUrl(studio))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(PortalMeetingsUrl(otherFirm))).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await otherFirm.Http.GetAsync(MeetingsUrl(studio))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(PortalMeetingsUrl(studio))).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await factory.CreateClient().GetAsync(MeetingsUrl(studio))).StatusCode);
    }

    [Fact]
    public async Task ArchivingTheClient_ClosesThePortalToItsMeetings_AndRestoringGivesThemBack()
    {
        var studio = await Studio();
        await ConnectGoogle(studio.Http);
        var elena = await studio.AddClient("Elena");
        await Schedule(studio, elena, "Kick-off", null, days: 1);
        var clientUrl = $"/api/workspaces/{studio.WorkspaceId}/clients/{elena.ClientId}";

        // An archived card is no longer a client of the firm at all, like for the projects.
        Assert.Equal(HttpStatusCode.OK, (await studio.Http.PostAsync($"{clientUrl}/archive", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await elena.Http.GetAsync(PortalMeetingsUrl(studio))).StatusCode);

        Assert.Equal(HttpStatusCode.OK, (await studio.Http.PostAsync($"{clientUrl}/restore", null)).StatusCode);
        Assert.Single((await elena.Http.GetFromJsonAsync<PortalMeetingResponse[]>(PortalMeetingsUrl(studio)))!);
    }

    private static string MeetingsUrl(StudioSession studio) => $"/api/workspaces/{studio.WorkspaceId}/meetings";

    private static string PortalMeetingsUrl(StudioSession studio) => $"/api/portal/workspaces/{studio.WorkspaceId}/meetings";

    private static object NewMeeting(ClientSession client, string title = "Kick-off", string? description = null, Guid? projectId = null, int days = 1)
    {
        var start = DateTimeOffset.UtcNow.AddDays(days);

        return new { clientId = client.ClientId, projectId, title, description, startsAt = start, endsAt = start.AddHours(1) };
    }

    private async Task<MeetingResponse> Schedule(StudioSession studio, ClientSession client, string title, Guid? projectId, int days)
    {
        var response = await studio.Http.PostAsJsonAsync(MeetingsUrl(studio), NewMeeting(client, title, projectId: projectId, days: days));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        return (await response.Content.ReadFromJsonAsync<MeetingResponse>())!;
    }

    // The OAuth round trip has its own tests (GoogleConnectionTests); here the account is just given a connected Google.
    // Returns the refresh token, which is also how the fake calendar tells organisers apart.
    private async Task<string> ConnectGoogle(HttpClient http)
    {
        var me = (await http.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!;
        var refreshToken = $"1//{Guid.NewGuid():N}";

        using var scope = factory.Services.CreateScope();
        var protectedToken = scope.ServiceProvider.GetRequiredService<ISecretProtector>().Protect("google-refresh-token", refreshToken);
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        db.GoogleConnections.Add(GoogleConnection.Connect(me.Id, $"ana-{Guid.NewGuid():N}@gmail.example", protectedToken, DateTime.UtcNow));
        await db.SaveChangesAsync();

        return refreshToken;
    }
}
