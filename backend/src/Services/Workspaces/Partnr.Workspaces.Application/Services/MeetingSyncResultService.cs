using Partnr.Contracts.Events;
using Partnr.Workspaces.Application.Interfaces.Repositories;
using Partnr.Workspaces.Application.Interfaces.Services;
using Partnr.Workspaces.Domain.Enums;

namespace Partnr.Workspaces.Application.Services;

internal sealed class MeetingSyncResultService(IMeetingRepository meetings, IUnitOfWork unitOfWork, TimeProvider time) : IMeetingSyncResultService
{
    public async Task ApplyAsync(MeetingSynced result, CancellationToken cancellationToken)
    {
        var meeting = await meetings.FindAsync(result.MeetingId, cancellationToken);
        if (meeting is null)
        {
            return;
        }

        var status = result.Outcome switch
        {
            MeetingSyncOutcome.Synced => MeetingSyncStatus.Synced,
            MeetingSyncOutcome.Failed => MeetingSyncStatus.Failed,
            _ => MeetingSyncStatus.NeedsReconnect,
        };

        if (!meeting.ApplySyncResult(result.Version, status, result.MeetUrl))
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ParkAsync(Guid organizerUserId, CancellationToken cancellationToken)
    {
        // What is in step with Google has nothing waiting, so it is left alone.
        foreach (var meeting in await meetings.ListOpenUnsyncedForOrganizerAsync(organizerUserId, Now(), cancellationToken))
        {
            meeting.MarkNeedsReconnect();
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ResumeAsync(Guid organizerUserId, CancellationToken cancellationToken)
    {
        foreach (var meeting in await meetings.ListOpenUnsyncedForOrganizerAsync(organizerUserId, Now(), cancellationToken))
        {
            meeting.ResumeSync(Now());
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
