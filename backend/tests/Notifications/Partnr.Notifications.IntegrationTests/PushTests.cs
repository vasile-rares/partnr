using System.Net.Http.Headers;
using System.Threading.Channels;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.DependencyInjection;
using Partnr.Contracts.Events;
using Partnr.Notifications.Api.Realtime;
using Partnr.Notifications.Application.Interfaces.Services;

namespace Partnr.Notifications.IntegrationTests;

// The live side of the inbox: a signal over the hub, with no data, to the accounts whose inbox changed.
[Collection(ApiCollection.Name)]
public sealed class PushTests(NotificationsFactory factory)
{
    private static readonly TimeSpan SignalWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan QuietWait = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task ANotification_SignalsItsRecipient_AndNobodyElse()
    {
        var elena = Guid.NewGuid();
        var studio = Guid.NewGuid();
        await using var forElena = await Listen(elena);
        await using var forStudio = await Listen(studio);

        await Tell(events => events.ApplyAsync(Requested(elena), CancellationToken.None));

        Assert.True(await forElena.NextAsync(SignalWait));
        Assert.False(await forElena.NextAsync(QuietWait), "one change, one signal");
        Assert.False(await forStudio.NextAsync(QuietWait));
    }

    [Fact]
    public async Task ARequestTakenBack_SignalsTheClient_BecauseItLeftTheirInbox()
    {
        var elena = Guid.NewGuid();
        var request = Requested(elena);
        await Tell(events => events.ApplyAsync(request, CancellationToken.None));
        await using var forElena = await Listen(elena);

        await Tell(events => events.WithdrawAsync(request.ApprovalRequestId, CancellationToken.None));

        Assert.True(await forElena.NextAsync(SignalWait));
    }

    [Fact]
    public async Task MarkingRead_SignalsTheAccountItself_SoItsOtherTabsFollow_AndMarkingAllAgainSaysNothing()
    {
        var elena = Guid.NewGuid();
        await Tell(events => events.ApplyAsync(Requested(elena), CancellationToken.None));
        await using var otherTab = await Listen(elena);
        using var http = factory.CreateClient();
        http.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", InternalTokens.Issue(elena.ToString()));

        await http.PostAsync("/api/notifications/read-all", null);
        Assert.True(await otherTab.NextAsync(SignalWait));

        // Nothing is unread now, so marking all again changes nothing and says nothing.
        await http.PostAsync("/api/notifications/read-all", null);
        Assert.False(await otherTab.NextAsync(QuietWait));
    }

    [Fact]
    public async Task AChangedConversation_SignalsTheseAccountsWithTheProjectsId()
    {
        var ana = Guid.NewGuid();
        var elena = Guid.NewGuid();
        var outsider = Guid.NewGuid();
        var project = Guid.NewGuid();
        await using var forAna = await Listen(ana);
        await using var forElena = await Listen(elena);
        await using var forOutsider = await Listen(outsider);

        await Tell(events => events.ApplyAsync(new ConversationChanged(project, [ana, elena]), CancellationToken.None));

        Assert.Equal(project, await forAna.NextConversationAsync(SignalWait));
        Assert.Equal(project, await forElena.NextConversationAsync(SignalWait));
        Assert.Null(await forOutsider.NextConversationAsync(QuietWait));
        Assert.False(await forAna.NextAsync(QuietWait), "a conversation is not an inbox change");
    }

    [Fact]
    public async Task TheHub_RefusesAnonymousVisitors()
    {
        await using var anonymous = new InboxListener(Connect(userId: null));

        await Assert.ThrowsAnyAsync<Exception>(() => anonymous.StartAsync());
    }

    private async Task Tell(Func<INotificationEventService, Task> action)
    {
        using var scope = factory.Services.CreateScope();
        await action(scope.ServiceProvider.GetRequiredService<INotificationEventService>());
    }

    private static ApprovalRequested Requested(Guid recipient) =>
        new(Guid.NewGuid(), Guid.NewGuid(), "Studio", Guid.NewGuid(), "Website", "Mockup", recipient, $"user-{recipient:N}@example.com", "Elena");

    private async Task<InboxListener> Listen(Guid userId)
    {
        var listener = new InboxListener(Connect(userId));
        await listener.StartAsync();

        return listener;
    }

    // The gateway puts the JWT on the handshake of the WebSocket too; here the test does.
    private HubConnection Connect(Guid? userId) =>
        new HubConnectionBuilder()
            .WithUrl(new Uri(factory.Server.BaseAddress, NotificationsHub.Route), options =>
            {
                options.Transports = HttpTransportType.WebSockets;
                options.SkipNegotiation = true;
                options.HttpMessageHandlerFactory = _ => factory.Server.CreateHandler();
                options.WebSocketFactory = (context, cancellationToken) =>
                {
                    var client = factory.Server.CreateWebSocketClient();
                    client.ConfigureRequest = request =>
                    {
                        if (userId is { } id)
                        {
                            request.Headers.Authorization = $"Bearer {InternalTokens.Issue(id.ToString())}";
                        }
                    };

                    return new ValueTask<System.Net.WebSockets.WebSocket>(client.ConnectAsync(context.Uri, cancellationToken));
                };
            })
            .Build();

    private sealed class InboxListener : IAsyncDisposable
    {
        private readonly HubConnection _connection;
        private readonly Channel<bool> _signals = Channel.CreateUnbounded<bool>();
        private readonly Channel<Guid> _conversations = Channel.CreateUnbounded<Guid>();

        public InboxListener(HubConnection connection)
        {
            _connection = connection;
            _connection.On(NotificationsHub.InboxChanged, () => _signals.Writer.TryWrite(true));
            _connection.On<Guid>(NotificationsHub.ConversationChanged, projectId => _conversations.Writer.TryWrite(projectId));
        }

        public Task StartAsync() => _connection.StartAsync();

        /// <summary>True when a signal arrived (and is taken) within the time.</summary>
        public async Task<bool> NextAsync(TimeSpan timeout)
        {
            using var cancel = new CancellationTokenSource(timeout);
            try
            {
                await _signals.Reader.ReadAsync(cancel.Token);

                return true;
            }
            catch (OperationCanceledException)
            {
                return false;
            }
        }

        /// <summary>The project whose conversation changed, when a signal arrived (and is taken) within the time; null otherwise.</summary>
        public async Task<Guid?> NextConversationAsync(TimeSpan timeout)
        {
            using var cancel = new CancellationTokenSource(timeout);
            try
            {
                return await _conversations.Reader.ReadAsync(cancel.Token);
            }
            catch (OperationCanceledException)
            {
                return null;
            }
        }

        public ValueTask DisposeAsync() => _connection.DisposeAsync();
    }
}
