using Partnr.Workspaces.Domain.Enums;
using Partnr.Workspaces.Domain.Exceptions;

namespace Partnr.Workspaces.Domain.Entities;

/// <summary>
/// A call with a client, organised by one member (whose Google Calendar holds the event). The client is fixed; there is no
/// draft: the client is invited the moment the event exists. The Google event is always brought to what the meeting says now.
/// </summary>
public sealed class Meeting : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 2000;
    public static readonly TimeSpan MinDuration = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan MaxDuration = TimeSpan.FromHours(8);

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

    /// <summary>Counts the changes a member made. An answer about the Google event is about one version and must not mark a later one as done; it is also the concurrency token.</summary>
    public int Version { get; private set; }

    /// <summary>Where the Google event stands, as the Integrations service last said (it owns the queue and the tokens).</summary>
    public MeetingSyncStatus SyncStatus { get; private set; }

    public string? MeetUrl { get; private set; }

    public bool IsCancelled => Status == MeetingStatus.Cancelled;

    public bool HasEndedAt(DateTime nowUtc) => EndsAtUtc <= nowUtc;

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
        meeting.Changed();

        return meeting;
    }

    /// <summary>Replaces everything but the client. Moving the meeting needs a start in the future; a title or a note can still be fixed while it runs.</summary>
    public void Update(Guid? projectId, string title, string? description, DateTime startsAtUtc, DateTime endsAtUtc, DateTime nowUtc)
    {
        EnsureOpen(nowUtc);
        Apply(projectId, title, description, startsAtUtc, endsAtUtc, nowUtc, timeChanged: startsAtUtc != StartsAtUtc || endsAtUtc != EndsAtUtc);
        Changed();
    }

    public void Cancel(DateTime nowUtc)
    {
        EnsureOpen(nowUtc);
        Status = MeetingStatus.Cancelled;
        CancelledAtUtc = nowUtc;
        Changed();
    }

    /// <summary>A member asks for another go after the tries ran out; the Integrations service is told separately.</summary>
    public void RetrySync(DateTime nowUtc)
    {
        if (SyncStatus != MeetingSyncStatus.Failed || HasEndedAt(nowUtc))
        {
            throw new DomainException("There is nothing to retry.");
        }

        SyncStatus = MeetingSyncStatus.Pending;
    }

    /// <summary>The organiser connected Google again: what waited for it is in the queue again.</summary>
    public void ResumeSync(DateTime nowUtc)
    {
        if (SyncStatus == MeetingSyncStatus.NeedsReconnect && !HasEndedAt(nowUtc))
        {
            SyncStatus = MeetingSyncStatus.Pending;
        }
    }

    /// <summary>The organiser's permission is gone. A meeting that is already in step with Google has nothing waiting, so it is left alone.</summary>
    public void MarkNeedsReconnect()
    {
        if (SyncStatus is MeetingSyncStatus.Pending or MeetingSyncStatus.Failed)
        {
            SyncStatus = MeetingSyncStatus.NeedsReconnect;
        }
    }

    /// <summary>
    /// What the Integrations service answered for one version of the meeting. An answer about an older version says nothing about
    /// what the meeting is now, so it is refused (false); the Meet link of the current one is kept.
    /// </summary>
    public bool ApplySyncResult(int version, MeetingSyncStatus status, string? meetUrl)
    {
        if (version != Version)
        {
            return false;
        }

        SyncStatus = status;
        MeetUrl = meetUrl ?? MeetUrl;

        return true;
    }

    // Every change makes the Google event out of date: the Integrations service is told (the event is published with the save).
    private void Changed()
    {
        Version++;
        SyncStatus = MeetingSyncStatus.Pending;
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
