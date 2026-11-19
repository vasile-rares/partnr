namespace Partnr.Notifications.Domain.Entities;

/// <summary>
/// An approval request that was taken back. Events of different kinds travel on different queues, so "taken back" can arrive before
/// "asked": this is what makes the late "asked" say nothing, instead of leaving a notification for something that no longer holds.
/// </summary>
public sealed class WithdrawnRequest : Entity
{
    private WithdrawnRequest()
    {
    }

    /// <summary>The id is the approval request's own.</summary>
    public DateTime WithdrawnAtUtc { get; private set; }

    public static WithdrawnRequest Create(Guid approvalRequestId, DateTime nowUtc) =>
        new() { Id = approvalRequestId, WithdrawnAtUtc = nowUtc };
}
