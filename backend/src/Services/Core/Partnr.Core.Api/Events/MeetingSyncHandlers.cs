using Partnr.Contracts.Events;
using Partnr.Core.Application.Interfaces.Services;

namespace Partnr.Core.Api.Events;

// What Integrations says about the meetings' Google events. Wolverine finds these by convention; delivery is at least once.
public static class MeetingSyncedHandler
{
    public static Task Handle(MeetingSynced message, IMeetingSyncResultService results, CancellationToken cancellationToken) =>
        results.ApplyAsync(message, cancellationToken);
}

public static class GoogleConnectedHandler
{
    public static Task Handle(GoogleConnected message, IMeetingSyncResultService results, CancellationToken cancellationToken) =>
        results.ResumeAsync(message.UserId, cancellationToken);
}

public static class GoogleDisconnectedHandler
{
    public static Task Handle(GoogleDisconnected message, IMeetingSyncResultService results, CancellationToken cancellationToken) =>
        results.ParkAsync(message.UserId, cancellationToken);
}
