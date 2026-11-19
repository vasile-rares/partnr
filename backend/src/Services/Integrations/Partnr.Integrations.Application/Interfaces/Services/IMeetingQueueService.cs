using Partnr.Contracts.Events;

namespace Partnr.Integrations.Application.Interfaces.Services;

/// <summary>What the meetings' events do to the queue. Events are delivered at least once and may arrive late, so every step is safe to repeat.</summary>
public interface IMeetingQueueService
{
    /// <summary>Queues a newer version of the meeting; an event that is not newer than the row changes nothing.</summary>
    Task ApplyAsync(MeetingChanged change, CancellationToken cancellationToken);

    /// <summary>Queues a meeting that gave up, if it still can be sent.</summary>
    Task RetryAsync(Guid meetingId, CancellationToken cancellationToken);
}
