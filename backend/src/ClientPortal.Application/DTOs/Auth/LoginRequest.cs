using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Auth;

public sealed record LoginRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}
