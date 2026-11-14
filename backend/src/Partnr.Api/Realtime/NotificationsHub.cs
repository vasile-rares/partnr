using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.SignalR;

namespace Partnr.Api.Realtime;

/// <summary>
/// One way: the server tells a signed-in browser that its own inbox changed. Nothing here can be called by a client,
/// and nobody joins a group: a message goes to an account by its id (the NameIdentifier claim of the cookie).
/// </summary>
[Authorize]
public sealed class NotificationsHub : Hub
{
    /// <summary>Under /api, so the web app's rewrite to the API also carries the WebSocket.</summary>
    public const string Route = "/api/hubs/notifications";

    public const string InboxChanged = "inboxChanged";

    /// <summary>Carries the project's id, so an open conversation knows it is the one to reload.</summary>
    public const string ConversationChanged = "conversationChanged";
}
