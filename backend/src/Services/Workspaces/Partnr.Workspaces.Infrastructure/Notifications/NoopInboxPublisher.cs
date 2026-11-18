using Partnr.Workspaces.Application.Interfaces.Services;

namespace Partnr.Workspaces.Infrastructure.Notifications;

/// <summary>What runs where nothing listens (the Api replaces it with the SignalR one).</summary>
internal sealed class NoopInboxPublisher : IInboxPublisher
{
    public Task InboxChangedAsync(IReadOnlyCollection<Guid> userIds, CancellationToken cancellationToken) => Task.CompletedTask;
}
