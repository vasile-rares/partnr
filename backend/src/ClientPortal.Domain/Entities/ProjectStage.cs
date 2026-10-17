using ClientPortal.Domain.Enums;
using ClientPortal.Domain.Exceptions;

namespace ClientPortal.Domain.Entities;

/// <summary>Only changed through <see cref="Project"/>.</summary>
public sealed class ProjectStage : Entity
{
    public const int NameMaxLength = 200;
    public const int DescriptionMaxLength = 2000;

    private ProjectStage()
    {
    }

    internal ProjectStage(Guid projectId, string name, string? description, DateOnly? dueDate, int position)
    {
        ProjectId = projectId;
        Position = position;
        Update(name, description, dueDate, StageStatus.ToDo);
    }

    public Guid ProjectId { get; private set; }

    public string Name { get; private set; } = string.Empty;

    public string? Description { get; private set; }

    public DateOnly? DueDate { get; private set; }

    public StageStatus Status { get; private set; }

    /// <summary>Where the stage sits in the project, from 0. The positions of a project's stages are always 0, 1, 2...</summary>
    public int Position { get; internal set; }

    internal void Update(string name, string? description, DateOnly? dueDate, StageStatus status)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            throw new DomainException("Stage name is required.");
        }

        Name = name.Trim();
        Description = string.IsNullOrWhiteSpace(description) ? null : description.Trim();
        DueDate = dueDate;
        Status = status;
    }
}
