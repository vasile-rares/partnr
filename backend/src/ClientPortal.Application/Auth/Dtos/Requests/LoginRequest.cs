using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.Auth.Dtos.Requests;

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
