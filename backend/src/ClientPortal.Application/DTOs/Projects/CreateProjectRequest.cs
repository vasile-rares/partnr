using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Projects;

/// <summary>A new project always starts in Planning; the status is changed afterwards, with the other details.</summary>
public sealed record CreateProjectRequest
{
    // Nullable so [Required] can tell a missing id from a real one.
    [Required]
    public Guid? ClientId { get; init; }

    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? DueDate { get; init; }
}
