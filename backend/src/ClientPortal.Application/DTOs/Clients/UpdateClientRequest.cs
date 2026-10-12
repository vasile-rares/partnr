using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Clients;

/// <summary>Replaces all editable details; an optional field left empty is cleared.</summary>
public sealed record UpdateClientRequest
{
    [Required]
    [MaxLength(200)]
    public string Name { get; init; } = string.Empty;

    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; init; } = string.Empty;

    [MaxLength(200)]
    public string? Company { get; init; }

    [MaxLength(50)]
    public string? Phone { get; init; }

    [MaxLength(2000)]
    public string? Notes { get; init; }
}
