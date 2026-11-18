using System.ComponentModel.DataAnnotations;

namespace Partnr.Contracts;

/// <summary>The key the gateway signs the internal JWT with and the services check it with (HS256: the same secret on both sides).</summary>
public sealed class InternalAuthOptions
{
    public const int MinimumKeyBytes = 32;

    [Required]
    public string SigningKey { get; set; } = "";

    public byte[] KeyBytes() => Convert.FromBase64String(SigningKey);

    public static bool IsValidKey(InternalAuthOptions options) =>
        Convert.TryFromBase64String(options.SigningKey, new byte[options.SigningKey.Length], out var written) && written >= MinimumKeyBytes;
}
