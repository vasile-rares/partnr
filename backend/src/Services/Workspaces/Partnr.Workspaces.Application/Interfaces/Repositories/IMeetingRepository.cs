using Partnr.Workspaces.Domain.Entities;

namespace Partnr.Workspaces.Application.Interfaces.Repositories;

/// <summary>Every lookup is scoped to a workspace: a meeting of another workspace is simply not found, even by id.</summary>
public interface IMeetingRepository
{
    Task<Meeting?> GetByIdAsync(Guid workspaceId, Guid id, CancellationToken cancellationToken);

    /// <summary>Soonest first.</summary>
    Task<IReadOnlyList<Meeting>> ListAsync(Guid workspaceId, bool includeCancelled, Guid? clientId, Guid? projectId, CancellationToken cancellationToken);

    /// <summary>What an account sees as a client of one firm: meetings of its own, linked, non-archived card, starting at or after <paramref name="since"/>. Soonest first.</summary>
    Task<IReadOnlyList<Meeting>> ListForClientAccountAsync(Guid workspaceId, Guid userId, DateTime since, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the meetings whose sync is due, for this worker only: it pushes their next try out by <paramref name="lease"/> in the same
    /// statement, so two workers never take the same row, and one that dies halfway gives it back after the lease. The meetings
    /// come back untracked, as a snapshot: the caller loads them again before it writes.
    /// </summary>
    Task<IReadOnlyList<Meeting>> ClaimDueForSyncAsync(DateTime nowUtc, TimeSpan lease, int limit, CancellationToken cancellationToken);

    /// <summary>The organiser's meetings that have not ended and are not in step with Google (tracked).</summary>
    Task<IReadOnlyList<Meeting>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken);

    void Add(Meeting meeting);
}
