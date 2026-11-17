using System.Net.Http.Json;
using System.Threading.Channels;
using Partnr.Api.Realtime;
using Microsoft.AspNetCore.Http.Connections;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.SignalR.Client;

namespace Partnr.IntegrationTests.ClientView;

// The live side of the inbox: a signal over the hub, with no data, to the accounts whose inbox changed.
public sealed partial class ClientProjectsTests
{
    private static readonly TimeSpan SignalWait = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan QuietWait = TimeSpan.FromMilliseconds(500);

    [Fact]
    public async Task AskingForApproval_SignalsTheClient_AndNobodyElse()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        await using var forElena = await Listen(elena.Email);
        await using var forStudio = await Listen(studio.Email);

        await studio.RequestApproval(project, mockup, null);

        Assert.True(await forElena.NextAsync(SignalWait));
        Assert.False(await forElena.NextAsync(QuietWait), "one change, one signal");
        Assert.False(await forStudio.NextAsync(QuietWait));
    }

    [Fact]
    public async Task TheClientsAnswer_SignalsTheFreelancerWhoAsked()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);
        await using var forStudio = await Listen(studio.Email);
        await using var forElena = await Listen(elena.Email);

        await elena.Http.PostAsJsonAsync(ApprovalUrl(studio, project, mockup, round.Id, "approve"), new { });

        Assert.True(await forStudio.NextAsync(SignalWait));
        Assert.False(await forElena.NextAsync(QuietWait));
    }

    [Fact]
    public async Task MarkingRead_SignalsTheAccountItself_SoItsOtherTabsFollow()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        await studio.RequestApproval(project, mockup, null);
        var notification = (await elena.Http.GetFromJsonAsync<Partnr.Application.DTOs.Notifications.NotificationsResponse>("/api/notifications"))!.Items.Single();
        await using var otherTab = await Listen(elena.Email);

        await elena.Http.PostAsync($"/api/notifications/{notification.Id}/read", null);
        Assert.True(await otherTab.NextAsync(SignalWait));

        // A second notification (its own signal), then "mark all": a change again.
        var other = await studio.AddDeliverable(project, "Other", null, share: true);
        await studio.RequestApproval(project, other, null);
        Assert.True(await otherTab.NextAsync(SignalWait));
        await elena.Http.PostAsync("/api/notifications/read-all", null);
        Assert.True(await otherTab.NextAsync(SignalWait));

        // Nothing is unread now, so marking all again changes nothing and says nothing.
        await elena.Http.PostAsync("/api/notifications/read-all", null);
        Assert.False(await otherTab.NextAsync(QuietWait));
    }

    [Fact]
    public async Task ARequestTakenBack_SignalsTheClient_BecauseItLeftTheirInbox()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        var round = await studio.RequestApproval(project, mockup, null);
        await using var forElena = await Listen(elena.Email);

        await studio.Http.PostAsync($"{studio.DeliverablesUrl(project)}/{mockup.Id}/approvals/{round.Id}/withdraw", null);

        Assert.True(await forElena.NextAsync(SignalWait));
    }

    [Fact]
    public async Task SendingTheEmail_IsNotAnInboxChange_AndPushesNothing()
    {
        var (studio, elena, project, mockup) = await SharedDeliverable("Elena");
        await using var forElena = await Listen(elena.Email);
        await studio.RequestApproval(project, mockup, null);
        Assert.True(await forElena.NextAsync(SignalWait));

        await EmailsTo(elena.Email, "asked for your approval"); // the worker's work: status and date of the email only

        Assert.False(await forElena.NextAsync(QuietWait));
    }

    [Fact]
    public async Task TheHub_RefusesAConnectionWithoutASession()
    {
        await using var anonymous = new InboxListener(Connect(cookie: null));

        await Assert.ThrowsAnyAsync<Exception>(() => anonymous.StartAsync());
    }

    private async Task<InboxListener> Listen(string email)
    {
        var listener = new InboxListener(Connect(await SessionCookie(email)));
        await listener.StartAsync();

        return listener;
    }

    // Signs in on a client that keeps no cookies of its own, so the cookie can be sent on the WebSocket's handshake too.
    private async Task<string> SessionCookie(string email)
    {
        using var http = factory.CreateClient(new WebApplicationFactoryClientOptions { HandleCookies = false });
        var response = await http.PostAsJsonAsync("/api/auth/login", new { email, password = Password });
        response.EnsureSuccessStatusCode();

        return string.Join("; ", response.Headers.GetValues("Set-Cookie").Select(cookie => cookie.Split(';')[0]));
    }

    private HubConnection Connect(string? cookie) =>
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
                        if (cookie is not null)
                        {
                            request.Headers.Cookie = cookie;
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
