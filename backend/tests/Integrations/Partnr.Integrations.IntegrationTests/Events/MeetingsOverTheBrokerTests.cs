using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Web;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Partnr.Integrations.Application.DTOs.Google;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Infrastructure.Persistence;
using Wolverine.EntityFrameworkCore;

namespace Partnr.Integrations.IntegrationTests.Events;

// The whole trip of a meeting: the service that owns it says it changed (RabbitMQ), the queue takes it in, the worker sends it to
// Google (the fake one) and the answer goes back the same way.
public sealed class MeetingsOverTheBrokerTests(IntegrationsBrokerFactory factory) : IClassFixture<IntegrationsBrokerFactory>
{
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task AMeetingThatChanges_IsSentToGoogle_AndTheMeetLinkComesBack()
    {
        var (client, userId) = SignedIn();
        await Connect(client);
        var change = NewChange(userId, "Kick-off");

        await Publish(change);

        var answer = await Arrives(change.MeetingId, change.Version);
        Assert.Equal(MeetingSyncOutcome.Synced, answer.Outcome);
        Assert.StartsWith("https://meet.google.com/", answer.MeetUrl);
    }

    [Fact]
    public async Task ANewerVersion_IsSentAgain_AndAnOlderOneArrivingLateChangesNothing()
    {
        var (client, userId) = SignedIn();
        await Connect(client);
        var first = NewChange(userId, "First");
        await Publish(first);
        await Arrives(first.MeetingId, 1);

        await Publish(first with { Version = 2, Title = "Second" });
        var second = await Arrives(first.MeetingId, 2);
        await Publish(first with { Version = 1, Title = "Old news" });
        var control = NewChange(userId, "Control");
        await Publish(control);
        await Arrives(control.MeetingId, 1);

        Assert.Equal(MeetingSyncOutcome.Synced, second.Outcome);
        Assert.Equal(2, factory.Received.MeetingsSynced.Count(e => e.MeetingId == first.MeetingId));
    }

    [Fact]
    public async Task WithoutAConnection_TheMeetingWaitsForOne_AndConnectingPutsItBackInTheQueue()
    {
        var (client, userId) = SignedIn();
        var change = NewChange(userId, "Waiting");
        await Publish(change);

        var waiting = await Arrives(change.MeetingId, 1);
        Assert.Equal(MeetingSyncOutcome.NeedsReconnect, waiting.Outcome);

        await Connect(client);

        var synced = await Arrives(change.MeetingId, 1, MeetingSyncOutcome.Synced);
        Assert.StartsWith("https://meet.google.com/", synced.MeetUrl);
        Assert.Contains(factory.Received.Connected, e => e.UserId == userId);
    }

    [Fact]
    public async Task Disconnecting_TellsTheOtherServices()
    {
        var (client, userId) = SignedIn();
        await Connect(client);

        (await client.DeleteAsync("/api/integrations/google")).EnsureSuccessStatusCode();

        await Eventually(() => factory.Received.Disconnected.Any(e => e.UserId == userId), "GoogleDisconnected did not arrive");
    }

    private async Task Connect(HttpClient client)
    {
        var code = $"code-{Guid.NewGuid():N}";
        factory.Google.Grants[code] = new GoogleGrant($"1//{Guid.NewGuid():N}", $"ana-{Guid.NewGuid():N}@gmail.example", true);
        var url = (await (await client.PostAsync("/api/integrations/google/connect", null)).Content.ReadFromJsonAsync<GoogleConnectUrlResponse>())!.Url;
        var state = HttpUtility.ParseQueryString(new Uri(url).Query)["state"]!;

        var back = await client.GetAsync($"/api/integrations/google/callback?code={code}&state={Uri.EscapeDataString(state)}");

        Assert.EndsWith("/account?google=connected", back.Headers.Location!.ToString());
    }

    // The way the service that owns the meeting does it: through its outbox, in the transaction of a save.
    private async Task Publish(MeetingChanged change)
    {
        using var scope = factory.Services.CreateScope();
        var outbox = scope.ServiceProvider.GetRequiredService<IDbContextOutbox<IntegrationsDbContext>>();
        await outbox.PublishAsync(change);
        await outbox.SaveChangesAndFlushMessagesAsync();
    }

    private async Task<MeetingSynced> Arrives(Guid meetingId, int version, MeetingSyncOutcome? outcome = null)
    {
        MeetingSynced? found = null;
        await Eventually(
            () => (found = factory.Received.MeetingsSynced.FirstOrDefault(e => e.MeetingId == meetingId && e.Version == version && (outcome is null || e.Outcome == outcome))) is not null,
            $"The answer for meeting {meetingId} (version {version}) did not arrive");

        return found!;
    }

    private static async Task Eventually(Func<bool> condition, string message)
    {
        var deadline = DateTime.UtcNow + Arrival;
        while (!condition())
        {
            if (DateTime.UtcNow >= deadline)
            {
                throw new TimeoutException($"{message} within {Arrival.TotalSeconds} s.");
            }

            await Task.Delay(100);
        }
    }

    private static MeetingChanged NewChange(Guid organizer, string title)
    {
        var start = DateTime.UtcNow.AddDays(1);

        return new MeetingChanged(Guid.NewGuid(), 1, organizer, title, null, start, start.AddHours(1), "client@acme.example", false);
    }

    // Redirects are not followed: the callback ends on a page of the web app, which is not running here. The JWT is what the gateway would add.
    private (HttpClient Client, Guid UserId) SignedIn()
    {
        var client = factory.CreateClient(new WebApplicationFactoryClientOptions { AllowAutoRedirect = false });
        var userId = Guid.NewGuid();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", InternalTokens.Issue(userId.ToString()));

        return (client, userId);
    }
}
