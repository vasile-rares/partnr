using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.Infrastructure.Notifications;

/// <summary>What runs where nothing listens (the Api replaces it with the SignalR one).</summary>
internal sealed class NoopConversationPublisher : IConversationPublisher
{
    public Task ConversationChangedAsync(IReadOnlyCollection<Guid> userIds, Guid projectId, CancellationToken cancellationToken) => Task.CompletedTask;
}
