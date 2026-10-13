using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Application.DTOs.Invitations;

/// <summary>Sign-up through an invitation. There is no email: it is the one the invitation was sent to, and cannot be changed.</summary>
public sealed record RegisterViaInvitationRequest
{
    [Required]
    [MaxLength(200)]
    public string FullName { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;

    [MaxLength(50)]
    public string? Phone { get; init; }

    [MaxLength(200)]
    public string? Company { get; init; }
}
