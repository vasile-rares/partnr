using System.ComponentModel.DataAnnotations;

namespace Partnr.Core.Application.DTOs.Members;

public sealed record InviteMemberRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(320)]
    public string Email { get; init; } = string.Empty;
}
