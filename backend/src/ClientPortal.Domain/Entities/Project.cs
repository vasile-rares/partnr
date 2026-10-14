using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>
/// Work done for one client of a workspace. The client is fixed at creation: moving a project to another client would
/// show its history in someone else's portal. Like clients, projects are archived instead of deleted.
/// </summary>
public sealed class Project : Entity
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 4000;

    private Project()
    {
    }

    private Project(Guid workspaceId, Guid clientId, DateTime createdAtUtc)
    {
        WorkspaceId = workspaceId;
        ClientId = clientId;
        CreatedAtUtc = createdAtUtc;
        Status = ProjectStatus.Planning;
    }

    public Guid WorkspaceId { get; private set; }

    public Guid ClientId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateOnly? StartDate { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public ProjectStatus Status { get; private set; }

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime? ArchivedAtUtc { get; private set; }

    public bool IsArchived => ArchivedAtUtc is not null;

    /// <summary>Starts in <see cref="ProjectStatus.Planning"/>, so the client sees nothing until the project is ready.</summary>
    public static Project Create(
        Guid workspaceId,
        Guid clientId,
        string name,
        string? description = null,
        DateOnly? startDate = null,
        DateOnly? dueDate = null)
    {
        if (workspaceId == Guid.Empty)
        {
            throw new DomainException("A project belongs to a workspace.");
        }

        if (clientId == Guid.Empty)
        {
            throw new DomainException("A project belongs to a client.");
        }

        var project = new Project(workspaceId, clientId, DateTime.UtcNow);
        project.UpdateDetails(name, description, startDate, dueDate);

        return project;
    }

    public void UpdateDetails(string name, string? description, DateOnly? startDate, DateOnly? dueDate)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Project name is required.");
        }

        if (startDate is not null && dueDate is not null && dueDate < startDate)
        {
            throw new DomainException("The due date cannot be before the start date.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        DueDate = dueDate;
    }

    /// <summary>Any status can follow any other; going back to Planning hides the project from the client again.</summary>
    public void ChangeStatus(ProjectStatus status) => Status = status;

    /// <summary>Hides the project from the default list and from the portal. Archiving twice keeps the first date.</summary>
    public void Archive() => ArchivedAtUtc ??= DateTime.UtcNow;

    public void Restore() => ArchivedAtUtc = null;
}
