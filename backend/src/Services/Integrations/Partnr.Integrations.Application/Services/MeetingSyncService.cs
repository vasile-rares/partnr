using Partnr.Contracts.Events;
using Partnr.Integrations.Application.Common;
using Partnr.Integrations.Application.Exceptions;
using Partnr.Integrations.Application.Interfaces.Repositories;
using Partnr.Integrations.Application.Interfaces.Services;
using Partnr.Integrations.Domain.Entities;
using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.Application.Services;

internal sealed class MeetingSyncService(
    IMeetingSyncRepository syncs,
    IGoogleConnectionRepository connections,
    IGoogleCalendarClient calendar,
    ISecretProtector protector,
    IEventPublisher events,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IMeetingSyncService
{
    private const int BatchSize = 10;

    // Longer than any sync takes; only matters if a worker dies halfway, when the row comes back after it.
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    public async Task<MeetingSyncRun> SyncDueAsync(CancellationToken cancellationToken)
    {
        var due = await syncs.ClaimDueAsync(Now(), Lease, BatchSize, cancellationToken);
        var synced = 0;
        var failed = 0;
        var needReconnect = 0;

        foreach (var snapshot in due)
        {
            // Read before the call: this is the version that is sent, whatever happens to the row while Google answers.
            var sentVersion = snapshot.Version;
            var (attempt, connection) = await TryAsync(snapshot, cancellationToken);

            // The row is read again: the snapshot is from before the call to Google, and a newer version may have come in since.
            var row = await syncs.GetByIdAsync(snapshot.Id, cancellationToken);
            if (row is null)
            {
                continue;
            }

            switch (attempt.Outcome)
            {
                case AttemptOutcome.Pushed:
                    row.RecordSynced(sentVersion, attempt.EventId, attempt.MeetUrl, Now());
                    synced++;
                    break;
                case AttemptOutcome.NeedsReconnect:
                    row.MarkNeedsReconnect();
                    connection?.MarkNeedsReconnect();
                    needReconnect++;
                    break;
                default:
                    row.RecordSyncFailure(sentVersion, Now());
                    failed++;
                    break;
            }

            await PublishIfSettledAsync(row, cancellationToken);

            try
            {
                // One save per row: a later failure must not forget that this one is done. The answer leaves with it.
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConflictException)
            {
                // A newer version came in at that very moment. The lease runs out and the row is synced again, from its new state.
            }
        }

        return new MeetingSyncRun(synced, failed, needReconnect);
    }

    // A try that will be repeated later says nothing; a final answer is told to the service that owns the meeting.
    private async Task PublishIfSettledAsync(MeetingSync row, CancellationToken cancellationToken)
    {
        if (!row.IsSettled)
        {
            return;
        }

        var outcome = row.Status switch
        {
            MeetingSyncStatus.Synced => MeetingSyncOutcome.Synced,
            MeetingSyncStatus.Failed => MeetingSyncOutcome.Failed,
            _ => MeetingSyncOutcome.NeedsReconnect,
        };

        await events.PublishAsync(new MeetingSynced(row.Id, row.Version, outcome, row.IsCancelled ? null : row.MeetUrl), cancellationToken);
    }

    // The connection is returned so a refused permission can be remembered on it.
    private async Task<(MeetingSyncAttempt Attempt, GoogleConnection? Connection)> TryAsync(MeetingSync snapshot, CancellationToken cancellationToken)
    {
        var connection = await connections.GetForUserAsync(snapshot.OrganizerUserId, cancellationToken);
        if (connection is not { Status: GoogleConnectionStatus.Connected })
        {
            return (new MeetingSyncAttempt(AttemptOutcome.NeedsReconnect), null);
        }

        var token = protector.Unprotect(GoogleConnectionService.RefreshTokenPurpose, connection.ProtectedRefreshToken);
        if (token is null)
        {
            return (new MeetingSyncAttempt(AttemptOutcome.NeedsReconnect), connection);
        }

        try
        {
            return (await PushAsync(snapshot, token, cancellationToken), connection);
        }
        catch (GoogleAccessRevokedException)
        {
            return (new MeetingSyncAttempt(AttemptOutcome.NeedsReconnect), connection);
        }
        catch (GoogleCalendarException)
        {
            return (new MeetingSyncAttempt(AttemptOutcome.Failed), connection);
        }
    }

    private async Task<MeetingSyncAttempt> PushAsync(MeetingSync snapshot, string token, CancellationToken cancellationToken)
    {
        if (snapshot.IsCancelled)
        {
            // A meeting cancelled before it ever reached Google has no event to delete.
            if (snapshot.GoogleEventId is not null)
            {
                await calendar.DeleteEventAsync(token, snapshot.GoogleEventId, cancellationToken);
            }

            return new MeetingSyncAttempt(AttemptOutcome.Pushed, snapshot.GoogleEventId);
        }

        var result = await calendar.UpsertEventAsync(
            token,
            snapshot.EventId,
            exists: snapshot.GoogleEventId is not null,
            new GoogleEventDetails(snapshot.Title, snapshot.Description, snapshot.StartsAtUtc, snapshot.EndsAtUtc, snapshot.ClientEmail),
            cancellationToken);

        return new MeetingSyncAttempt(AttemptOutcome.Pushed, result.EventId, result.MeetUrl);
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
