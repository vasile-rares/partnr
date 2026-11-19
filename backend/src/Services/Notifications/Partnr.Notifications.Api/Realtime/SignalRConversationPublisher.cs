using Partnr.Notifications.Application.Interfaces.Services;
using Microsoft.AspNetCore.SignalR;

namespace Partnr.Notifications.Api.Realtime;

internal sealed class SignalRConversationPublisher(IHubContext<NotificationsHub> hub, ILogger<SignalRConversationPublisher> logger)
    : IConversationPublisher
{
    public async Task ConversationChangedAsync(IReadOnlyCollection<Guid> userIds, Guid projectId, CancellationToken cancellationToken)
    {
        try
        {
            await hub.Clients.Users(userIds.Select(id => id.ToString()).ToList())
                .SendAsync(NotificationsHub.ConversationChanged, projectId, cancellationToken);
        }
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            logger.LogWarning(ex, "Could not push the change of conversation {ProjectId}.", projectId);
        }
    }
}
