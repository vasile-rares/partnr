using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Infrastructure.Email;

/// <summary>The "Email" configuration section. Validated at startup, so a missing setting fails fast instead of on the first send.</summary>
public sealed class EmailOptions
{
    public const string SectionName = "Email";

    /// <summary>The sender, e.g. "Client Portal &lt;no-reply@example.com&gt;".</summary>
    [Required]
    public string From { get; init; } = string.Empty;

    [Required]
    public string SmtpHost { get; init; } = string.Empty;

    [Range(1, 65535)]
    public int SmtpPort { get; init; } = 587;

    /// <summary>Optional: Mailpit needs no login; real providers do.</summary>
    public string? SmtpUsername { get; init; }

    public string? SmtpPassword { get; init; }

    /// <summary>Require TLS. Off only for a local catcher such as Mailpit.</summary>
    public bool UseTls { get; init; } = true;
}
