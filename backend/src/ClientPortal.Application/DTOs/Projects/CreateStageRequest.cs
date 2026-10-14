using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Projects;

/// <summary>A new stage goes to the end of the project and starts as To do; it is moved and its status changed afterwards.</summary>
public sealed record CreateStageRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }

    public DateOnly? DueDate { get; init; }
}
