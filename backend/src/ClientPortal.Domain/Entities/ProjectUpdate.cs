using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>A note the freelancer writes about where the project stands. Its own aggregate, like a deliverable.</summary>
public sealed class ProjectUpdate : Entity
{
    public const int TitleMaxLength = 200;
    public const int BodyMaxLength = 4000;

    private ProjectUpdate()
    {
    }

    private ProjectUpdate(Guid workspaceId, Guid projectId, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        ProjectId = projectId;
        CreatedAtUtc = createdAtUtc;
        Status = ProjectUpdateStatus.Draft;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ProjectId { get; private set; }

    public string Title { get; private set; } = string.Empty;

    public string? Body { get; private set; }

    public ProjectUpdateStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    /// <summary>Set on the first share and kept afterwards; the client sees this date and the list is ordered by it.</summary>
    public DateTime? SharedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    public static ProjectUpdate Create(Guid workspaceId, Guid projectId, string title, string? body)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("An update belongs to a workspace.");
        }

        if (projectId == Guid.Empty)
        {
            throw new DomainException("An update belongs to a project.");
        }

        var update = new ProjectUpdate(workspaceId, projectId, DateTime.UtcNow);
        update.UpdateDetails(title, body);

        return update;
    }

    public void UpdateDetails(string title, string? body)
    {
        if (string.IsNullOrWhiteSpace(title))
        {
            throw new DomainException("Update title is required.");
        }

        Title = title.Trim();
        Body = string.IsNullOrWhiteSpace(body) ? null : body.Trim();
    }

    public void Share()
    {
        SharedAtUtc ??= DateTime.UtcNow;
        Status = ProjectUpdateStatus.Shared;
    }

    public void Unshare() => Status = ProjectUpdateStatus.Draft;

    /// <summary>Also back to Draft, so a restored update is never visible to the client without sharing it again.</summary>
    public void Archive()
    {
        ArchivedAtUtc ??= DateTime.UtcNow;
        Status = ProjectUpdateStatus.Draft;
    }

    public void Restore() => ArchivedAtUtc = null;
}
