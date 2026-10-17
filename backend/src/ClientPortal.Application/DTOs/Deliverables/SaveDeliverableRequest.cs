using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Deliverables;

/// <summary>Used to create and to update: an update replaces every field, and an optional one left empty is cleared. A new deliverable is a draft.</summary>
public sealed record SaveDeliverableRequest
{
    public Guid? StageId { get; init; }

    [Required]
    [MaxLength(200)]
    public string Title { get; init; } = string.Empty;

    [MaxLength(4000)]
    public string? Description { get; init; }

    [Required]
    [MaxLength(2000)]
    public string Url { get; init; } = string.Empty;
}
