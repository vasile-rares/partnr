using Partnr.Contracts.Events;
using Partnr.Integrations.Application.Interfaces.Services;

namespace Partnr.Integrations.Api.Events;

// Wolverine finds these by convention. Delivery is at least once, so the queue ignores what it already has.
public static class MeetingChangedHandler
{
    public static Task Handle(MeetingChanged message, IMeetingQueueService queue, CancellationToken cancellationToken) =>
        queue.ApplyAsync(message, cancellationToken);
}

public static class MeetingSyncRetryRequestedHandler
{
    public static Task Handle(MeetingSyncRetryRequested message, IMeetingQueueService queue, CancellationToken cancellationToken) =>
        queue.RetryAsync(message.MeetingId, cancellationToken);
}
