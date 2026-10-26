using System.Globalization;
using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>Work for one client, fixed at creation so its history never shows up in someone else's portal.</summary>
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

    /// <summary>The database returns stages in no particular order, so the position decides.</summary>
    public IReadOnlyList<ProjectStage> Stages => _stages.OrderBy(s => s.Position).ToList();

    public int DoneStageCount => _stages.Count(s => s.Status == StageStatus.Done);

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

        foreach (var stage in _stages.Where(s => s.DueDate is not null && !IsWithin(s.DueDate.Value, startDate, dueDate)))
        {
            throw new DomainException(
                $"The stage \"{stage.Name}\" is due on {Format(stage.DueDate!.Value)}, outside these dates. Change the stage first.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        StartDate = startDate;
        DueDate = dueDate;
    }

    /// <summary>Going back to Planning hides the project from the client again.</summary>
    public void ChangeStatus(ProjectStatus status) => Status = status;

    public ProjectStage AddStage(string name, string? description, DateOnly? dueDate)
    {
        EnsureStageDateFits(dueDate);
        var stage = new ProjectStage(Id, name, description, dueDate, _stages.Count);
        _stages.Add(stage);

        return stage;
    }

    public void UpdateStage(Guid stageId, string name, string? description, DateOnly? dueDate, StageStatus status)
    {
        var stage = RequireStage(stageId);
        EnsureStageDateFits(dueDate);
        stage.Update(name, description, dueDate, status);
    }

    public void RemoveStage(Guid stageId)
    {
        _stages.Remove(RequireStage(stageId));
        Renumber();
    }

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

    /// <summary>A stage cannot end before the project starts or after it is due, whichever of those dates the project has.</summary>
    private void EnsureStageDateFits(DateOnly? date)
    {
        if (date is null)
        {
            return;
        }

        if (StartDate is not null && date < StartDate)
        {
            throw new DomainException($"A stage cannot be due before the project starts ({Format(StartDate.Value)}).");
        }

        if (DueDate is not null && date > DueDate)
        {
            throw new DomainException($"A stage cannot be due after the project is due ({Format(DueDate.Value)}).");
        }
    }

    private static bool IsWithin(DateOnly date, DateOnly? start, DateOnly? end) => (start is null || date >= start) && (end is null || date <= end);

    private static string Format(DateOnly date) => date.ToString("d MMM yyyy", CultureInfo.InvariantCulture);

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
