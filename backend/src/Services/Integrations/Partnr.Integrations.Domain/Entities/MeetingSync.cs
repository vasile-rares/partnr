using Partnr.Integrations.Domain.Enums;

namespace Partnr.Integrations.Domain.Entities;

/// <summary>
/// What the calendar has to say about one meeting, and how far sending it got. The meeting itself belongs to another service: this row
/// is built from the events it publishes, and the row is the queue (see <see cref="Status"/> and <see cref="NextSyncAtUtc"/>). The
/// Google event is always brought to what the newest version says.
/// </summary>
public sealed class MeetingSync : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int MaxSyncAttempts = 5;

    // The wait after the 1st, 2nd, 3rd and 4th failed try; the 5th gives up.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    private MeetingSync()
    {
    }

    /// <summary>The id is the meeting's own, so the event in Google can be made from it and a repeated event finds the same row.</summary>
    public Guid OrganizerUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime StartsAtUtc { get; private set; }

    public DateTime EndsAtUtc { get; private set; }

    /// <summary>The client, invited to the event by Google.</summary>
    public string ClientEmail { get; private set; } = string.Empty;

    public bool IsCancelled { get; private set; }

    /// <summary>The version of the meeting this row is about; also the concurrency token, so a change that arrives while a try is in flight cannot be undone by its result.</summary>
    public int Version { get; private set; }

    public MeetingSyncStatus Status { get; private set; }

    public int SyncAttempts { get; private set; }

    /// <summary>When the worker may try next; null unless <see cref="Status"/> is Pending.</summary>
    public DateTime? NextSyncAtUtc { get; private set; }

    /// <summary>Null until the event exists in Google. It is made from the meeting's id, so a try whose answer was lost cannot create a second event.</summary>
    public string? GoogleEventId { get; private set; }

    public string? MeetUrl { get; private set; }

    /// <summary>What the event is called in Google.</summary>
    public string EventId => Id.ToString("N");

    public bool HasEndedAt(DateTime nowUtc) => EndsAtUtc <= nowUtc;

    public static MeetingSync Create(Guid meetingId)
    {
        if (meetingId == Guid.Empty)
        {
            throw new ArgumentException("A meeting has an id.", nameof(meetingId));
        }

        return new MeetingSync { Id = meetingId, Version = 0 };
    }

    /// <summary>
    /// Takes in a newer version of the meeting and queues it. An event that is not newer than what is here (a repeat, or one that
    /// arrived late) changes nothing: the answer is false.
    /// </summary>
    public bool Apply(
        int version,
        Guid organizerUserId,
        string title,
        string? description,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        string clientEmail,
        bool cancelled,
        DateTime nowUtc)
    {
        if (version <= Version)
        {
            return false;
        }

        Version = version;
        OrganizerUserId = organizerUserId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
        ClientEmail = clientEmail.Trim();
        IsCancelled = cancelled;
        Queue(nowUtc);

        return true;
    }

    /// <summary>A member asks for another go after the tries ran out. Anything else has nothing to retry.</summary>
    public bool Retry(DateTime nowUtc)
    {
        if (Status != MeetingSyncStatus.Failed || HasEndedAt(nowUtc))
        {
            return false;
        }

        Queue(nowUtc);

        return true;
    }

    /// <summary>The organiser connected Google again: what waited for it goes back in the queue.</summary>
    public bool Resume(DateTime nowUtc)
    {
        if (Status != MeetingSyncStatus.NeedsReconnect || HasEndedAt(nowUtc))
        {
            return false;
        }

        Queue(nowUtc);

        return true;
    }

    /// <summary>The organiser's permission is gone. What is already in step with Google has nothing waiting, so it is left alone.</summary>
    public bool MarkNeedsReconnect()
    {
        if (Status is not (MeetingSyncStatus.Pending or MeetingSyncStatus.Failed))
        {
            return false;
        }

        Status = MeetingSyncStatus.NeedsReconnect;
        NextSyncAtUtc = null;

        return true;
    }

    /// <summary>
    /// What Google answered for the version that was sent. The event is kept either way; the row counts as in step only if no newer
    /// version arrived meanwhile, otherwise it goes round again.
    /// </summary>
    public void RecordSynced(int syncedVersion, string? googleEventId, string? meetUrl, DateTime nowUtc)
    {
        GoogleEventId = googleEventId ?? GoogleEventId;
        MeetUrl = meetUrl ?? MeetUrl;

        if (syncedVersion != Version)
        {
            Queue(nowUtc);
            return;
        }

        Status = MeetingSyncStatus.Synced;
        SyncAttempts = 0;
        NextSyncAtUtc = null;
    }

    public void RecordSyncFailure(int syncedVersion, DateTime nowUtc)
    {
        if (syncedVersion != Version)
        {
            Queue(nowUtc);
            return;
        }

        SyncAttempts++;
        if (SyncAttempts >= MaxSyncAttempts || HasEndedAt(nowUtc))
        {
            Status = MeetingSyncStatus.Failed;
            NextSyncAtUtc = null;
            return;
        }

        Status = MeetingSyncStatus.Pending;
        NextSyncAtUtc = nowUtc + RetryDelays[SyncAttempts - 1];
    }

    /// <summary>The meeting is told about the answer when the sync reached a final state: Synced, Failed or NeedsReconnect, not a try that will be repeated.</summary>
    public bool IsSettled => Status != MeetingSyncStatus.Pending;

    private void Queue(DateTime nowUtc)
    {
        Status = MeetingSyncStatus.Pending;
        SyncAttempts = 0;
        NextSyncAtUtc = nowUtc;
    }
}
