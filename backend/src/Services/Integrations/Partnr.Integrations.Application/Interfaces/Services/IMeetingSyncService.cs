namespace Partnr.Integrations.Application.Interfaces.Services;

public interface IMeetingSyncService
{
    /// <summary>Brings the Google events of the meetings that are due to what they say now. A failure of one never stops the others.</summary>
    Task<MeetingSyncRun> SyncDueAsync(CancellationToken cancellationToken);
}

public sealed record MeetingSyncRun(int Synced, int Failed, int NeedReconnect);
