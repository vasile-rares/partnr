namespace Partnr.Workspaces.Application.Interfaces.Services;

/// <summary>Tells connected browsers that a project's conversation changed. Only the project's id travels; the browser asks for the rest.</summary>
public interface IConversationPublisher
{
    /// <summary>Never throws: a missed push is repaired the next time the page loads or the connection comes back.</summary>
    Task ConversationChangedAsync(IReadOnlyCollection<Guid> userIds, Guid projectId, CancellationToken cancellationToken);
}
