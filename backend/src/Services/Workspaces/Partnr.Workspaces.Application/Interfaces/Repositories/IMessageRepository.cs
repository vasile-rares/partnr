using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Interfaces.Repositories;

/// <summary>Messages are looked up by project; the services only ask after the project was found through the right door.</summary>
public interface IMessageRepository
{
    Task<Message?> GetByIdAsync(Guid projectId, Guid id, CancellationToken cancellationToken);

    /// <summary>Up to <paramref name="limit"/> messages written before <paramref name="beforeId"/> (or the newest), oldest first.</summary>
    Task<MessagePage> ListPageAsync(Guid projectId, Guid? beforeId, int limit, CancellationToken cancellationToken);

    /// <summary>The newest message of each project; projects with none are absent.</summary>
    Task<IReadOnlyDictionary<Guid, Message>> GetLatestPerProjectAsync(IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    /// <summary>
    /// Per project, the messages someone else wrote after this person's read mark (all of them without one), deleted ones left out.
    /// Projects with none are absent.
    /// </summary>
    Task<IReadOnlyDictionary<Guid, int>> CountUnreadPerProjectAsync(Guid userId, IReadOnlyCollection<Guid> projectIds, CancellationToken cancellationToken);

    void Add(Message message);
}

public sealed record MessagePage(IReadOnlyList<Message> Messages, bool HasEarlier);
