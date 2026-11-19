using Partnr.Integrations.Domain.Entities;

namespace Partnr.Integrations.Application.Interfaces.Repositories;

public interface IMeetingSyncRepository
{
    /// <summary>The row of a meeting, by the meeting's own id (tracked).</summary>
    Task<MeetingSync?> GetByIdAsync(Guid meetingId, CancellationToken cancellationToken);

    /// <summary>
    /// Takes the rows whose sync is due, for this worker only: it pushes their next try out by <paramref name="lease"/> in the same
    /// statement, so two workers never take the same row, and one that dies halfway gives it back after the lease. The rows
    /// come back untracked, as a snapshot: the caller loads them again before it writes.
    /// </summary>
    Task<IReadOnlyList<MeetingSync>> ClaimDueAsync(DateTime nowUtc, TimeSpan lease, int limit, CancellationToken cancellationToken);

    /// <summary>The organiser's meetings that have not ended and are not in step with Google (tracked).</summary>
    Task<IReadOnlyList<MeetingSync>> ListOpenUnsyncedForOrganizerAsync(Guid organizerUserId, DateTime nowUtc, CancellationToken cancellationToken);

    void Add(MeetingSync meetingSync);
}
