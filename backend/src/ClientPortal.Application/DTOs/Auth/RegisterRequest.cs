using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Auth;

public sealed record RegisterRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [Required]
    [MaxLength(200)]
    public string FullName { get; init; } = string.Empty;
}
