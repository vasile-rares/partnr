using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Projects;

/// <summary>Replaces all editable details of a stage, the status included; an optional field left empty is cleared.</summary>
public sealed record UpdateStageRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public DateOnly? DueDate { get; init; }

    [Required]
    [AllowedValues("ToDo", "InProgress", "Done")]
    public string Status { get; init; } = string.Empty;
}
