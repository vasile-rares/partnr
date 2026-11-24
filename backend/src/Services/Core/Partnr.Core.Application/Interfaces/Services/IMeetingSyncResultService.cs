using Partnr.Contracts.Events;

namespace Partnr.Core.Application.Interfaces.Services;

/// <summary>
/// What the Integrations service says about the meetings' Google events. The queue and the tokens are its; here only the state shown
/// to the people is kept. Events are delivered at least once and may arrive late, so every step is safe to repeat.
/// </summary>
public interface IMeetingSyncResultService
{
    /// <summary>Applies a final answer to the meeting it is about, unless the meeting has moved on to a newer version.</summary>
    Task ApplyAsync(MeetingSynced result, CancellationToken cancellationToken);

    /// <summary>The organiser removed Google: what was still waiting to be sent waits for a new connection.</summary>
    Task ParkAsync(Guid organizerUserId, CancellationToken cancellationToken);

    /// <summary>The organiser connected Google again: what waited for it is in the queue again.</summary>
    Task ResumeAsync(Guid organizerUserId, CancellationToken cancellationToken);
}
