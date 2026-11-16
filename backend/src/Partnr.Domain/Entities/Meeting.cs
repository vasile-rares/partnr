using Partnr.Domain.Enums;
using Partnr.Domain.Exceptions;

namespace Partnr.Domain.Entities;

/// <summary>
/// A call with a client, organised by one member (whose Google Calendar holds the event). The client is fixed; there is no
/// draft: the client is invited the moment the event exists. The Google event is always brought to what the meeting says now.
/// </summary>
public sealed class Meeting : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public const int MaxSyncAttempts = 5;
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

    // The wait after the 1st, 2nd, 3rd and 4th failed try; the 5th gives up.
    private static readonly TimeSpan[] RetryDelays = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(15), TimeSpan.FromMinutes(60)];

    private Meeting()
    {
    }

    private Meeting(Guid workspaceId, Guid clientId, Guid organizerUserId, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        ClientId = clientId;
        OrganizerUserId = organizerUserId;
        CreatedAtUtc = createdAtUtc;
        Status = MeetingStatus.Scheduled;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ClientId { get; private set; }

    /// <summary>Optional: a first talk can happen before there is a project.</summary>
    public Guid? ProjectId { get; private set; }

    public Guid OrganizerUserId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateTime StartsAtUtc { get; private set; }

    public DateTime EndsAtUtc { get; private set; }

    public MeetingStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? CancelledAtUtc { get; private set; }

    /// <summary>Counts the changes a member made. A sync that started at one version must not mark a later one as done, and it is also the concurrency token.</summary>
    public int Version { get; private set; }

    public MeetingSyncStatus SyncStatus { get; private set; }

    public int SyncAttempts { get; private set; }

    /// <summary>When the worker may try next; null unless <see cref="SyncStatus"/> is Pending.</summary>
    public DateTime? NextSyncAtUtc { get; private set; }

    /// <summary>Null until the event exists in Google. It is made from the meeting's id, so a try that Google answered but we never saw cannot make a second event.</summary>
    public string? GoogleEventId { get; private set; }

    public string? MeetUrl { get; private set; }

    public bool IsCancelled => Status == MeetingStatus.Cancelled;

    public bool HasEndedAt(DateTime nowUtc) => EndsAtUtc <= nowUtc;

    /// <summary>What the event is called in Google.</summary>
    public string EventId => Id.ToString("N");

    public static Meeting Schedule(
        Guid workspaceId,
        Guid clientId,
        Guid organizerUserId,
        Guid? projectId,
        string title,
        string? description,
        DateTime startsAtUtc,
        DateTime endsAtUtc,
        DateTime nowUtc)
    {
        if (workspaceId == Guid.Empty || clientId == Guid.Empty || organizerUserId == Guid.Empty)
        {
            throw new DomainException("A meeting needs a workspace, a client and someone to organise it.");
        }

        var meeting = new Meeting(workspaceId, clientId, organizerUserId, nowUtc);
        meeting.Apply(projectId, title, description, startsAtUtc, endsAtUtc, nowUtc, timeChanged: true);
        meeting.Changed(nowUtc);

        return meeting;
    }

    /// <summary>Replaces everything but the client. Moving the meeting needs a start in the future; a title or a note can still be fixed while it runs.</summary>
    public void Update(Guid? projectId, string title, string? description, DateTime startsAtUtc, DateTime endsAtUtc, DateTime nowUtc)
    {
        EnsureOpen(nowUtc);
        Apply(projectId, title, description, startsAtUtc, endsAtUtc, nowUtc, timeChanged: startsAtUtc != StartsAtUtc || endsAtUtc != EndsAtUtc);
        Changed(nowUtc);
    }

    public void Cancel(DateTime nowUtc)
    {
        EnsureOpen(nowUtc);
        Status = MeetingStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        Changed(nowUtc);
    }

    /// <summary>A member asks for another go after the tries ran out.</summary>
    public void RetrySync(DateTime nowUtc)
    {
        if (SyncStatus != MeetingSyncStatus.Failed || HasEndedAt(nowUtc))
        {
            throw new DomainException("There is nothing to retry.");
        }

        Queue(nowUtc);
    }

    /// <summary>The organiser connected Google again: what waited for it goes back in the queue.</summary>
    public void ResumeSync(DateTime nowUtc)
    {
        if (SyncStatus == MeetingSyncStatus.NeedsReconnect && !HasEndedAt(nowUtc))
        {
            Queue(nowUtc);
        }
    }

    /// <summary>The organiser's permission is gone. A meeting that is already in step with Google has nothing waiting, so it is left alone.</summary>
    public void MarkNeedsReconnect()
    {
        if (SyncStatus is MeetingSyncStatus.Pending or MeetingSyncStatus.Failed)
        {
            SyncStatus = MeetingSyncStatus.NeedsReconnect;
            NextSyncAtUtc = null;
        }
    }

    /// <summary>
    /// What Google answered for the version that was sent. The event is kept either way; the meeting counts as in step only if
    /// nobody changed it meanwhile, otherwise it goes round again.
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

        SyncStatus = MeetingSyncStatus.Synced;
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
            SyncStatus = MeetingSyncStatus.Failed;
            NextSyncAtUtc = null;
            return;
        }

        SyncStatus = MeetingSyncStatus.Pending;
        NextSyncAtUtc = nowUtc + RetryDelays[SyncAttempts - 1];
    }

    private void Changed(DateTime nowUtc)
    {
        Version++;
        Queue(nowUtc);
    }

    private void Queue(DateTime nowUtc)
    {
        SyncStatus = MeetingSyncStatus.Pending;
        SyncAttempts = 0;
        NextSyncAtUtc = nowUtc;
    }

    private void EnsureOpen(DateTime nowUtc)
    {
        if (IsCancelled)
        {
            throw new DomainException("This meeting was cancelled.");
        }

        if (HasEndedAt(nowUtc))
        {
            throw new DomainException("This meeting already took place.");
        }
    }

    private void Apply(Guid? projectId, string title, string? description, DateTime startsAtUtc, DateTime endsAtUtc, DateTime nowUtc, bool timeChanged)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Meeting title is required.");
        }

        if (projectId == Guid.Empty)
        {
            throw new DomainException("The project of a meeting cannot be empty.");
        }

        var length = endsAtUtc - startsAtUtc;
        if (length < MinDuration || length > MaxDuration)
        {
            throw new DomainException("A meeting lasts between 5 minutes and 8 hours.");
        }

        if (timeChanged && startsAtUtc <= nowUtc)
        {
            throw new DomainException("A meeting has to start in the future.");
        }

        ProjectId = projectId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartsAtUtc = startsAtUtc;
        EndsAtUtc = endsAtUtc;
    }
}
