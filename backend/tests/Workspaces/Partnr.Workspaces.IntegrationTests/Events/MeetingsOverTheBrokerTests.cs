using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.DTOs.Auth;
using Partnr.Workspaces.Application.DTOs.Clients;
using Partnr.Workspaces.Application.DTOs.Meetings;
using Partnr.Workspaces.Application.DTOs.Workspaces;
using Partnr.Workspaces.Application.Exceptions;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Domain.Entities;
using Partnr.Workspaces.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;

namespace Partnr.Workspaces.IntegrationTests.Events;

// The meetings' side of the trip to Google: the service says a meeting changed (RabbitMQ), and what the Integrations service answers
// comes back the same way and shows on the meeting. The Integrations service is played by the test.
public sealed class MeetingsOverTheBrokerTests(BrokerApiFactory factory) : IClassFixture<BrokerApiFactory>
{
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(20);

    [Fact]
    public async Task ScheduleChangeAndCancel_EachSayMeetingChanged_WithEverythingTheCalendarNeeds()
    {
        var (http, workspaceId, userId) = await SignedInWithWorkspace();
        var client = await AddClient(http, workspaceId, $"elena-{Guid.NewGuid():N}@example.com");
        var start = DateTimeOffset.UtcNow.AddDays(1);

        var created = await Schedule(http, workspaceId, client.Id, "Kick-off", start);
        var first = await ArrivesAt(created.Id, 1);
        Assert.Equal((userId, "Kick-off", client.Email, false), (first.OrganizerUserId, first.Title, first.ClientEmail, first.Cancelled));
        Assert.Equal(start.UtcDateTime, first.StartsAtUtc, TimeSpan.FromSeconds(1));

        (await http.PutAsJsonAsync($"/api/workspaces/{workspaceId}/meetings/{created.Id}", new { title = "Moved", startsAt = start.AddDays(1), endsAt = start.AddDays(1).AddHours(1) })).EnsureSuccessStatusCode();
        Assert.Equal("Moved", (await ArrivesAt(created.Id, 2)).Title);

        (await http.PostAsync($"/api/workspaces/{workspaceId}/meetings/{created.Id}/cancel", null)).EnsureSuccessStatusCode();
        Assert.True((await ArrivesAt(created.Id, 3)).Cancelled);
    }

    [Fact]
    public async Task TheAnswerOfIntegrations_ArrivingOverTheBroker_ShowsOnTheMeeting()
    {
        var (http, workspaceId, _) = await SignedInWithWorkspace();
        var client = await AddClient(http, workspaceId, $"elena-{Guid.NewGuid():N}@example.com");
        var meeting = await Schedule(http, workspaceId, client.Id, "Kick-off", DateTimeOffset.UtcNow.AddDays(1));
        await ArrivesAt(meeting.Id, 1);

        await PublishAsIntegrations(new MeetingSynced(meeting.Id, 1, MeetingSyncOutcome.Synced, "https://meet.google.com/abc-defg-hij"));

        var shown = await Eventually(async () =>
        {
            var current = (await http.GetFromJsonAsync<MeetingResponse>($"/api/workspaces/{workspaceId}/meetings/{meeting.Id}"))!;
            return current.SyncStatus == "Synced" ? current : null;
        });
        Assert.Equal("https://meet.google.com/abc-defg-hij", shown.MeetUrl);
    }

    [Fact]
    public async Task GoogleDisconnected_ArrivingOverTheBroker_ParksTheOrganisersMeetings()
    {
        var (http, workspaceId, userId) = await SignedInWithWorkspace();
        var client = await AddClient(http, workspaceId, $"elena-{Guid.NewGuid():N}@example.com");
        var meeting = await Schedule(http, workspaceId, client.Id, "Kick-off", DateTimeOffset.UtcNow.AddDays(1));

        await PublishAsIntegrations(new GoogleDisconnected(userId));

        await Eventually(async () =>
            (await http.GetFromJsonAsync<MeetingResponse>($"/api/workspaces/{workspaceId}/meetings/{meeting.Id}"))!.SyncStatus == "NeedsReconnect" ? "parked" : null);

        await PublishAsIntegrations(new GoogleConnected(userId));

        await Eventually(async () =>
            (await http.GetFromJsonAsync<MeetingResponse>($"/api/workspaces/{workspaceId}/meetings/{meeting.Id}"))!.SyncStatus == "Pending" ? "resumed" : null);
    }

    [Fact]
    public async Task AnEventOfASaveThatFails_IsNeverSent()
    {
        var (http, workspaceId, userId) = await SignedInWithWorkspace();
        var start = DateTime.UtcNow.AddDays(1);
        var lost = new MeetingChanged(Guid.NewGuid(), 1, userId, "Never saved", null, start, start.AddHours(1), "elena@example.com", false);
        var sameEmail = $"twice-{Guid.NewGuid():N}@example.com";

        // The event is published, then the save is refused (the same email twice in one workspace): the outbox row goes with the rollback.
        using (var scope = factory.Services.CreateScope())
        {
            var events = scope.ServiceProvider.GetRequiredService<IEventPublisher>();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            await events.PublishAsync(lost);
            db.Clients.Add(Client.Register(workspaceId, "One", sameEmail));
            db.Clients.Add(Client.Register(workspaceId, "Two", sameEmail));

            await Assert.ThrowsAsync<ConflictException>(() => scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync());
        }

        // A control that does go out, sent after: once it has arrived, the lost one would have too.
        var client = await AddClient(http, workspaceId, $"elena-{Guid.NewGuid():N}@example.com");
        var control = await Schedule(http, workspaceId, client.Id, "Control", DateTimeOffset.UtcNow.AddDays(1));
        await ArrivesAt(control.Id, 1);
        Assert.DoesNotContain(factory.Received.MeetingsChanged, e => e.MeetingId == lost.MeetingId);
    }

    private async Task<MeetingChanged> ArrivesAt(Guid meetingId, int version) =>
        await Eventually(() => Task.FromResult(factory.Received.MeetingsChanged.FirstOrDefault(e => e.MeetingId == meetingId && e.Version == version)));

    private async Task<T> Eventually<T>(Func<Task<T?>> find)
        where T : class
    {
        var deadline = DateTime.UtcNow + Arrival;
        while (DateTime.UtcNow < deadline)
        {
            if (await find() is { } found)
            {
                return found;
            }

            await Task.Delay(100);
        }

        throw new TimeoutException($"What was waited for did not happen within {Arrival.TotalSeconds} s.");
    }

    // The way the other service does it: through its outbox, in the transaction of a save (here this service's own database context stands in).
    private async Task PublishAsIntegrations<T>(T message)
        where T : notnull
    {
        using var scope = factory.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<ApplicationDbContext>>();
        await outbox.PublishAsync(message);
        await outbox.SaveChangesAndFlushMessagesAsync();
    }

    private static async Task<MeetingResponse> Schedule(HttpClient http, Guid workspaceId, Guid clientId, string title, DateTimeOffset start)
    {
        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/meetings", new { clientId, title, startsAt = start, endsAt = start.AddHours(1) });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<MeetingResponse>())!;
    }

    private static async Task<ClientResponse> AddClient(HttpClient http, Guid workspaceId, string email)
    {
        var response = await http.PostAsJsonAsync($"/api/workspaces/{workspaceId}/clients", new { name = "Elena", email });
        response.EnsureSuccessStatusCode();

        return (await response.Content.ReadFromJsonAsync<ClientResponse>())!;
    }

    private async Task<(HttpClient Http, Guid WorkspaceId, Guid UserId)> SignedInWithWorkspace()
    {
        var http = factory.CreateClient();
        var registered = await http.PostAsJsonAsync(
            "/api/auth/register", new { email = $"user-{Guid.NewGuid():N}@example.com", password = "correct horse", fullName = "Test User" });
        registered.EnsureSuccessStatusCode();
        var me = (await http.GetFromJsonAsync<CurrentUserResponse>("/api/auth/me"))!;
        factory.Google.Connect(me.Id);

        var response = await http.PostAsJsonAsync("/api/workspaces", new { name = "Studio" });
        response.EnsureSuccessStatusCode();

        return (http, (await response.Content.ReadFromJsonAsync<WorkspaceResponse>())!.Id, me.Id);
    }
}
