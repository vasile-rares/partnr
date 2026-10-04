using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.Clients.Dtos.Requests;

public sealed record RegisterClientRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; init; } = string.Empty;
}
