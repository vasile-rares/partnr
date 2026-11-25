using Partnr.Core.Domain.Entities;

namespace Partnr.Core.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a meeting of another workspace is simply not found, even by id.</summary>
public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>Soonest first.</summary>
    Task<IReadOnlyList<Meeting>> ListAsync(Guid workspaceId, bool includeCancelled, Guid? clientId, Guid? projectId, CancellationToken cancellationToken);

    /// <summary>What an account sees as a client of one firm: meetings of its own, linked, non-archived card, starting at or after <paramref name="since"/>. Soonest first.</summary>
    Task<IReadOnlyList<Meeting>> ListForClientAccountAsync(Guid workspaceId, Guid userId, DateTime since, CancellationToken cancellationToken);

    /// <summary>A meeting by its id alone (tracked), for what another service says about it: that message knows the meeting, not its workspace.</summary>
    Task<Meeting?> FindAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>The organiser's meetings that have not ended and are not in step with Google (tracked).</summary>
    Task<IReadOnlyList<Meeting>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken);

    /// <summary>How many meetings of this workspace the member organises that are not cancelled and have not ended.</summary>
    Task<int> CountUpcomingForOrganizerAsync(Guid workspaceId, Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken);

    void Add(Meeting meeting);
}
