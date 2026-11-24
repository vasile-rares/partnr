using System.ComponentModel.DataAnnotations;

namespace Partnr.Core.Application.DTOs.Approvals;

public sealed record ApproveRequest
{
    [MaxLength(2000)]
    public string? Message { get; init; }
}

/// <summary>Asking for changes without saying which would leave the freelancer guessing.</summary>
public sealed record RequestChangesRequest
{
    [Required]
    [MaxLength(2000)]
    public string Message { get; init; } = string.Empty;
}
