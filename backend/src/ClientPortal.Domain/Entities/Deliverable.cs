using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>Something handed to the client as a link. Its own aggregate: feedback and approvals will hang off it.</summary>
public sealed class Deliverable : Entity
{
    public const int TitleMaxLength = 200;
    public const int DescriptionMaxLength = 4000;
    public const int UrlMaxLength = 2000;

    private Deliverable()
    {
    }

    private Deliverable(Guid workspaceId, Guid projectId, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        CreatedAtUtc = createdAtUtc;
        Status = DeliverableStatus.Draft;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    /// <summary>Emptied by the database when the stage is deleted; the deliverable stays.</summary>
    public Guid? StageId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public string Url { get; private set; } = string.Empty;

    public DeliverableStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public static Deliverable Create(
        Guid workspaceId,
        Guid projectId,
        Guid? stageId,
        string title,
        string? description,
        string url)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("A deliverable belongs to a workspace.");
        }

        if (projectId == Guid.Empty)
        {
            throw new DomainException("A deliverable belongs to a project.");
        }

        var deliverable = new Deliverable(workspaceId, projectId, DateTime.UtcNow);
        deliverable.UpdateDetails(stageId, title, description, url);

        return deliverable;
    }

    public void UpdateDetails(Guid? stageId, string title, string? description, string url)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Deliverable title is required.");
        }

        // https only: the client opens this link, so javascript: or data: must never get through.
        if (!Uri.TryCreate(url?.Trim(), UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
        {
            throw new DomainException("The link must be a valid https address.");
        }

        StageId = stageId;
        Title = title.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        Url = uri.AbsoluteUri;
    }

    public void Share() => Status = DeliverableStatus.Shared;

    public void Unshare() => Status = DeliverableStatus.Draft;

    /// <summary>Also back to Draft, so a restored deliverable is never visible to the client without sharing it again.</summary>
    public void Archive()
    {
        ArchivedAtUtc ??= DateTime.UtcNow;
        Status = DeliverableStatus.Draft;
    }

    public void Restore() => ArchivedAtUtc = null;
}
