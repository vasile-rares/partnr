using Partnr.Contracts.Events;
using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Domain.Entities;

namespace Partnr.Integrations.Application.Services;

internal sealed class MeetingQueueService(IMeetingSyncRepository syncs, IUnitOfWork unitOfWork, TimeProvider time) : IMeetingQueueService
{
    public async Task ApplyAsync(MeetingChanged change, CancellationToken cancellationToken)
    {
        var row = await syncs.GetByIdAsync(change.MeetingId, cancellationToken);
        var isNew = row is null;
        row ??= MeetingSync.Create(change.MeetingId);

        var applied = row.Apply(
            change.Version,
            change.OrganizerUserId,
            change.Title,
            change.Description,
            change.StartsAtUtc,
            change.EndsAtUtc,
            change.ClientEmail,
            change.Cancelled,
            time.GetUtcNow().UtcDateTime);
        if (!applied)
        {
            return;
        }

        if (isNew)
        {
            syncs.Add(row);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RetryAsync(Guid meetingId, CancellationToken cancellationToken)
    {
        var row = await syncs.GetByIdAsync(meetingId, cancellationToken);
        if (row is null || !row.Retry(time.GetUtcNow().UtcDateTime))
        {
            return;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }
}
