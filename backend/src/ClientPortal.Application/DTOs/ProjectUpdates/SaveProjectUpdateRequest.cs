using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.ProjectUpdates;

/// <summary>Used to create and to update: an update replaces every field, and an empty body is cleared. A new update is a draft.</summary>
public sealed record SaveProjectUpdateRequest
{
    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Body { get; init; }
}
