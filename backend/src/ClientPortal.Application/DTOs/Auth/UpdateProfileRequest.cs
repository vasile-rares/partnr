using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Auth;

/// <summary>No email: it cannot be changed yet.</summary>
public sealed record UpdateProfileRequest
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? Phone { get; init; }

    [MaxLength(200)]
    public string? Company { get; init; }
}
