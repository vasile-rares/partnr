using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.ProjectFiles;

/// <summary>The text fields sent next to the file in the upload form. A blank name becomes the file's own name. A new file is a draft.</summary>
public sealed record UploadProjectFileRequest
{
    [MaxLength(200)]
    public string? Name { get; init; }

    [MaxLength(2000)]
    public string? Description { get; init; }
}
