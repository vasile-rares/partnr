using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.ProjectFiles;

/// <summary>Edits what people see about a file; the file itself is never replaced. An empty description is cleared.</summary>
public sealed record SaveProjectFileRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [MaxLength(2000)]
    public string? Description { get; init; }
}
