using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Partnr.Notifications.Application.DTOs.Notifications;
using Partnr.Notifications.Application.Interfaces.Services;

namespace Partnr.Notifications.IntegrationTests;

/// <summary>The account's own inbox, through the API the gateway forwards to, signed the way the gateway signs (a JWT, no cookie here).</summary>
[Collection(ApiCollection.Name)]
public sealed class InboxTests(NotificationsFactory factory)
{
    [Fact]
    public async Task EveryRoute_NeedsASignedInAccount()
    {
        using var anonymous = factory.CreateClient();

        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.GetAsync("/api/notifications/unread-count")).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, (await anonymous.PostAsync("/api/notifications/read-all", null)).StatusCode);
    }

    [Fact]
    public async Task AnEventMakesANotification_WithTheTextTheEmailHas_AndALinkIntoTheApp()
    {
        var me = Guid.NewGuid();
        using var client = SignedIn(me);
        var request = await Tell(Requested(me));

        var inbox = (await client.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!;

        Assert.Equal(1, inbox.Unread);
        var item = Assert.Single(inbox.Items);
        Assert.Equal("Studio asked for your approval: Mockup", item.Title);
        Assert.Equal($"/w/{request.WorkspaceId}/projects/{request.ProjectId}?tab=deliverables", item.Path);
        Assert.False(item.IsRead);
    }

    [Fact]
    public async Task ReadingOne_ThenAll_BringsTheCountDown_AndTheRoutesNeverShowSomeoneElses()
    {
        var me = Guid.NewGuid();
        var someoneElse = Guid.NewGuid();
        using var client = SignedIn(me);
        using var theirs = SignedIn(someoneElse);
        await Tell(Requested(me));
        await Tell(Requested(me));
        var other = await Tell(Requested(someoneElse));
        var mine = (await client.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items;

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync($"/api/notifications/{mine[0].Id}/read", null)).StatusCode);
        Assert.Equal(1, (await client.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count"))!.Unread);

        Assert.Equal(HttpStatusCode.NoContent, (await client.PostAsync("/api/notifications/read-all", null)).StatusCode);
        Assert.Equal(0, (await client.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count"))!.Unread);
        Assert.Equal(1, (await theirs.GetFromJsonAsync<UnreadCountResponse>("/api/notifications/unread-count"))!.Unread);

        var theirItem = (await theirs.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items.Single();
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/notifications/{theirItem.Id}/read", null)).StatusCode);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/notifications/{Guid.NewGuid()}/read", null)).StatusCode);
        Assert.NotEqual(Guid.Empty, other.ApprovalRequestId);
    }

    [Fact]
    public async Task ARequestTakenBack_LeavesTheInbox()
    {
        var me = Guid.NewGuid();
        using var client = SignedIn(me);
        var request = await Tell(Requested(me));
        var item = (await client.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items.Single();

        await WithScope(events => events.WithdrawAsync(request.ApprovalRequestId, CancellationToken.None));

        Assert.Empty((await client.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items);
        Assert.Equal(HttpStatusCode.NotFound, (await client.PostAsync($"/api/notifications/{item.Id}/read", null)).StatusCode);
    }

    [Fact]
    public async Task TheSameEventTwice_IsOneNotification()
    {
        var me = Guid.NewGuid();
        using var client = SignedIn(me);
        var request = Requested(me);

        await Tell(request);
        await Tell(request);

        Assert.Single((await client.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items);
    }

    [Fact]
    public async Task ADeliveryThatRacesItsTwin_NeitherFailsNorDoubles()
    {
        var me = Guid.NewGuid();
        using var client = SignedIn(me);
        var request = Requested(me);

        // Both deliveries may find nothing yet and try to insert: the unique key lets one win and the other is told so.
        var results = await Task.WhenAll(Enumerable.Range(0, 5).Select(_ => TryTell(request)));

        Assert.Contains(results, ok => ok);
        Assert.Single((await client.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!.Items);
    }

    private async Task<bool> TryTell(ApprovalRequested request)
    {
        try
        {
            await Tell(request);

            return true;
        }
        catch (Partnr.Notifications.Application.Exceptions.ConflictException)
        {
            // The loser of the race: the message is delivered again and finds the notification there.
            return false;
        }
    }

    private async Task<ApprovalRequested> Tell(ApprovalRequested request)
    {
        await WithScope(events => events.ApplyAsync(request, CancellationToken.None));

        return request;
    }

    private async Task WithScope(Func<INotificationEventService, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<INotificationEventService>());
    }

    private static ApprovalRequested Requested(Guid recipient) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", recipient, $"user-{recipient:N}@example.com", "Elena");

    private HttpClient SignedIn(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", InternalTokens.Issue(userId.ToString()));

        return client;
    }
}
