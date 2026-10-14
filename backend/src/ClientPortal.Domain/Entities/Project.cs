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

    private readonly List<ProjectStage> _stages = [];

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

    /// <summary>The stages in order. The database returns them in no particular order, so the position decides.</summary>
    public IReadOnlyList<ProjectStage> Stages => _stages.OrderBy(s => s.Position).ToList();

    public int DoneStageCount => _stages.Count(s => s.Status == StageStatus.Done);

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

    /// <summary>Adds a stage at the end, still to do.</summary>
    public ProjectStage AddStage(string name, string? description, DateOnly? dueDate)
    {
        var stage = new ProjectStage(Id, name, description, dueDate, _stages.Count);
        _stages.Add(stage);

        return stage;
    }

    public void UpdateStage(Guid stageId, string name, string? description, DateOnly? dueDate, StageStatus status) =>
        RequireStage(stageId).Update(name, description, dueDate, status);

    /// <summary>Removes the stage and closes the gap, so the positions stay 0, 1, 2...</summary>
    public void RemoveStage(Guid stageId)
    {
        _stages.Remove(RequireStage(stageId));
        Renumber();
    }

    /// <summary>Swaps the stage with its neighbour. At either end there is nothing to swap with, and nothing changes.</summary>
    public void MoveStage(Guid stageId, bool up)
    {
        var ordered = Stages;
        var stage = RequireStage(stageId);
        var index = ordered.ToList().IndexOf(stage);
        var neighbourIndex = up ? index - 1 : index + 1;
        if (neighbourIndex < 0 || neighbourIndex >= ordered.Count)
        {
            return;
        }

        var neighbour = ordered[neighbourIndex];
        (stage.Position, neighbour.Position) = (neighbour.Position, stage.Position);
    }

    public bool HasStage(Guid stageId) => _stages.Any(s => s.Id == stageId);

    private ProjectStage RequireStage(Guid stageId) =>
        _stages.FirstOrDefault(s => s.Id == stageId) ?? throw new DomainException("This project has no such stage.");

    private void Renumber()
    {
        var position = 0;
        foreach (var stage in Stages)
        {
            stage.Position = position++;
        }
    }

    /// <summary>Hides the project from the default list and from the portal. Archiving twice keeps the first date.</summary>
    public void Archive() => ArchivedAtUtc ??= DateTime.UtcNow;

    public void Restore() => ArchivedAtUtc = null;
}
