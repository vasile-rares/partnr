using Partnr.Workspaces.Application.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace Partnr.Workspaces.Api.Realtime;

internal sealed class SignalRInboxPublisher(IHubContext<NotificationsHub> hub, ILogger<SignalRInboxPublisher> logger) : IInboxPublisher
{
    public async Task InboxChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Users(userIds.Select(id => id.ToString()).ToList()).SendAsync(NotificationsHub.InboxChanged, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not push the inbox change to {Count} account(s).", userIds.Count);
        }
    }
}
