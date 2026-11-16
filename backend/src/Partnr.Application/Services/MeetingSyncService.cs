using Partnr.Application.Common;
using Partnr.Application.Exceptions;
using Partnr.Application.Interfaces.Repositories;
using Partnr.Application.Interfaces.Services;
using Partnr.Domain.Entities;
using Partnr.Domain.Enums;

namespace Partnr.Application.Services;

internal sealed class MeetingSyncService(
    IMeetingRepository meetings,
    IClientRepository clients,
    IGoogleConnectionRepository connections,
    IGoogleCalendarClient calendar,
    ISecretProtector protector,
    IUnitOfWork unitOfWork,
    TimeProvider time) : IMeetingSyncService
{
    private const int BatchSize = 10;

    // Longer than any sync takes; only matters if a worker dies halfway, when the row comes back after it.
    private static readonly TimeSpan Lease = TimeSpan.FromMinutes(5);

    public async Task<MeetingSyncRun> SyncDueAsync(CancellationToken cancellationToken)
    {
        var due = await meetings.ClaimDueForSyncAsync(Now(), Lease, BatchSize, cancellationToken);
        var synced = 0;
        var failed = 0;
        var needReconnect = 0;

        foreach (var snapshot in due)
        {
            // Read before the call: this is the version that is sent, whatever happens to the meeting while Google answers.
            var sentVersion = snapshot.Version;
            var (attempt, connection) = await TryAsync(snapshot, cancellationToken);

            // The meeting is read again: the snapshot is from before the call to Google, and a member may have changed it since.
            var meeting = await meetings.GetByIdAsync(snapshot.WorkspaceId, snapshot.Id, cancellationToken);
            if (meeting is null)
            {
                continue;
            }

            switch (attempt.Outcome)
            {
                case MeetingSyncOutcome.Pushed:
                    meeting.RecordSynced(sentVersion, attempt.EventId, attempt.MeetUrl, Now());
                    synced++;
                    break;
                case MeetingSyncOutcome.NeedsReconnect:
                    meeting.MarkNeedsReconnect();
                    connection?.MarkNeedsReconnect();
                    needReconnect++;
                    break;
                default:
                    meeting.RecordSyncFailure(sentVersion, Now());
                    failed++;
                    break;
            }

            try
            {
                // One save per meeting: a later failure must not forget that this one is done.
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
            catch (ConflictException)
            {
                // Someone changed the meeting at that very moment. The lease runs out and it is synced again, from its new state.
            }
        }

        return new MeetingSyncRun(synced, failed, needReconnect);
    }

    // The connection is returned so a refused permission can be remembered on it.
    private async Task<(MeetingSyncAttempt Attempt, GoogleConnection? Connection)> TryAsync(Meeting snapshot, CancellationToken cancellationToken)
    {
        var connection = await connections.GetForUserAsync(snapshot.OrganizerUserId, cancellationToken);
        if (connection is not { Status: GoogleConnectionStatus.Connected })
        {
            return (new MeetingSyncAttempt(MeetingSyncOutcome.NeedsReconnect), null);
        }

        var token = protector.Unprotect(GoogleConnectionService.RefreshTokenPurpose, connection.ProtectedRefreshToken);
        if (token is null)
        {
            return (new MeetingSyncAttempt(MeetingSyncOutcome.NeedsReconnect), connection);
        }

        try
        {
            return (await PushAsync(snapshot, token, cancellationToken), connection);
        }
        catch (GoogleAccessRevokedException)
        {
            return (new MeetingSyncAttempt(MeetingSyncOutcome.NeedsReconnect), connection);
        }
        catch (GoogleCalendarException)
        {
            return (new MeetingSyncAttempt(MeetingSyncOutcome.Failed), connection);
        }
    }

    private async Task<MeetingSyncAttempt> PushAsync(Meeting snapshot, string token, CancellationToken cancellationToken)
    {
        if (snapshot.IsCancelled)
        {
            // A meeting cancelled before it ever reached Google has no event to delete.
            if (snapshot.GoogleEventId is not null)
            {
                await calendar.DeleteEventAsync(token, snapshot.GoogleEventId, cancellationToken);
            }

            return new MeetingSyncAttempt(MeetingSyncOutcome.Pushed, snapshot.GoogleEventId);
        }

        var client = await clients.GetByIdAsync(snapshot.WorkspaceId, snapshot.ClientId, cancellationToken)
            ?? throw new GoogleCalendarException("The meeting's client was not found.");
        var result = await calendar.UpsertEventAsync(
            token,
            snapshot.EventId,
            exists: snapshot.GoogleEventId is not null,
            new GoogleEventDetails(snapshot.Title, snapshot.Description, snapshot.StartsAtUtc, snapshot.EndsAtUtc, client.Email),
            cancellationToken);

        return new MeetingSyncAttempt(MeetingSyncOutcome.Pushed, result.EventId, result.MeetUrl);
    }

    private DateTime Now() => time.GetUtcNow().UtcDateTime;
}
