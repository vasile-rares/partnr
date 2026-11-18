using System.ComponentModel.DataAnnotations;

namespace Partnr.Gateway.Sessions;

internal sealed class InternalAuthOptions
{
    public const int MinimumKeyBytes = 32;

    [Required]
    public string SigningKey { get; set; } = "";

    public byte[] KeyBytes() => Convert.FromBase64String(SigningKey);

    public static bool IsValidKey(InternalAuthOptions options) =>
        Convert.TryFromBase64String(options.SigningKey, new byte[options.SigningKey.Length], out var written) && written >= MinimumKeyBytes;
}
