using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Projects;

/// <summary>Replaces all editable details, the status included; an optional field left empty is cleared. The client cannot change.</summary>
public sealed record UpdateProjectRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; init; }

    public DateOnly? StartDate { get; init; }

    public DateOnly? DueDate { get; init; }

    [Required]
    [AllowedValues("Planning", "Active", "OnHold", "Completed")]
    public string Status { get; init; } = string.Empty;
}
