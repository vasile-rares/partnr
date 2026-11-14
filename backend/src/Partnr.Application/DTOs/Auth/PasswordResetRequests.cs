using System.ComponentModel.DataAnnotations;
using Partnr.Domain.Common;

namespace Partnr.Application.DTOs.Auth;

public sealed record ForgotPasswordRequest
{
    [Required]
    [EmailAddress]
    [MaxLength(256)]
    public string Email { get; init; } = string.Empty;
}

public sealed record ResetPasswordRequest
{
    [Required]
    [MaxLength(SecureToken.MaxLength)]
    public string Token { get; init; } = string.Empty;

    [Required]
    [MinLength(8)]
    [MaxLength(128)]
    public string Password { get; init; } = string.Empty;
}

/// <summary>What the reset page shows before a new password is typed: whose account it is.</summary>
public sealed record PasswordResetPreviewResponse(string Email);
