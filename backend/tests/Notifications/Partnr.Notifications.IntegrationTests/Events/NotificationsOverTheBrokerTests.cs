using System.Net.Http.Headers;
using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Partnr.Notifications.Application.DTOs.Notifications;
using Wolverine;

namespace Partnr.Notifications.IntegrationTests.Events;

// The whole trip of what the other services say: RabbitMQ carries it, the service stores it, the inbox shows it and the email goes out.
public sealed class NotificationsOverTheBrokerTests(NotificationsBrokerFactory factory) : IClassFixture<NotificationsBrokerFactory>
{
    private static readonly TimeSpan Arrival = TimeSpan.FromSeconds(30);

    [Fact]
    public async Task ARequestForApproval_ArrivingOverTheBroker_ShowsInTheInbox_AndIsEmailedToTheAddressItNamed()
    {
        var elena = Guid.NewGuid();
        var email = $"elena-{Guid.NewGuid():N}@example.com";
        using var client = SignedIn(elena);

        await Publish(new ApprovalRequested(Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", elena, email, "Elena Client"));

        var item = (await Eventually(async () => (await Inbox(client)).Items.FirstOrDefault()))!;
        Assert.Equal("Studio asked for your approval: Mockup", item.Title);
        var message = (await Eventually(() => Task.FromResult(factory.Emails.SentTo(email).FirstOrDefault())))!;
        Assert.Equal("Studio asked for your approval: Mockup", message.Subject);
        Assert.Contains("Hi Elena Client,", message.TextBody);
    }

    [Fact]
    public async Task AnInvitation_AndAPasswordReset_ArriveOverTheBroker_AsEmailsWithTheirLinks()
    {
        var invited = $"elena-{Guid.NewGuid():N}@example.com";
        var account = $"ana-{Guid.NewGuid():N}@example.com";

        await Publish(new ClientInvited(invited, "Elena", "Studio", "Ana Pop", "invite-token", DateTime.UtcNow.AddDays(7)));
        await Publish(new PasswordResetRequested(account, "Ana Pop", "reset-token"));

        var invitation = (await Eventually(() => Task.FromResult(factory.Emails.SentTo(invited).FirstOrDefault())))!;
        Assert.Contains("/invite/invite-token", invitation.TextBody);
        var reset = (await Eventually(() => Task.FromResult(factory.Emails.SentTo(account).FirstOrDefault())))!;
        Assert.Contains("/reset-password/reset-token", reset.TextBody);
    }

    [Fact]
    public async Task TheClientsAnswer_AndAnAcceptedInvitation_ArriveTheSameWay()
    {
        var ana = Guid.NewGuid();
        using var client = SignedIn(ana);
        var round = Guid.NewGuid();

        await Publish(new ApprovalDecided(round, ApprovalDecision.ChangesRequested, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", "Elena", ana, "ana@example.com", "Ana"));
        await Publish(new InvitationAccepted(Guid.NewGuid(), "Studio", Guid.NewGuid(), "Elena", ana, "ana@example.com", "Ana"));

        var inbox = await Eventually(async () =>
        {
            var current = await Inbox(client);
            return current.Items.Count == 2 ? current : null;
        });
        Assert.Contains(inbox!.Items, item => item.Title == "Elena requested changes to Mockup");
        Assert.Contains(inbox.Items, item => item.Title == "Elena joined your client portal");
    }

    [Fact]
    public async Task ARequestTakenBackThatArrivesFirst_LeavesNothingWhenTheOneThatAskedFollows()
    {
        var elena = Guid.NewGuid();
        using var client = SignedIn(elena);
        var round = Guid.NewGuid();
        var email = $"elena-{Guid.NewGuid():N}@example.com";

        await Publish(new ApprovalRequestWithdrawn(round));
        await Task.Delay(1000); // the withdrawal is taken in before the request is sent
        await Publish(new ApprovalRequested(round, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", elena, email, "Elena"));
        var control = Guid.NewGuid();
        await Publish(new ApprovalRequested(control, Guid.NewGuid(), "Studio", Guid.NewGuid(), "Control", "Control", elena, email, "Elena"));

        var inbox = await Eventually(async () =>
        {
            var current = await Inbox(client);
            return current.Items.Count > 0 ? current : null;
        });
        Assert.Single(inbox!.Items);
        Assert.Contains("Control", inbox.Items[0].Title);
        Assert.DoesNotContain(inbox.Items, item => item.Title.Contains("Mockup"));
    }

    private async Task Publish<T>(T message)
        where T : notnull
    {
        using var scope = factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(message);
    }

    private static async Task<NotificationsResponse> Inbox(HttpClient client) =>
        (await client.GetFromJsonAsync<NotificationsResponse>("/api/notifications"))!;

    private static async Task<T?> Eventually<T>(Func<Task<T?>> find)
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

    private HttpClient SignedIn(Guid userId)
    {
        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", InternalTokens.Issue(userId.ToString()));

        return client;
    }
}
