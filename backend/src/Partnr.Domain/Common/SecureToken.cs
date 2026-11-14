using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Partnr.Domain.Common;

/// <summary>The links sent by email (invitations, password resets): the token goes in the link, only its hash is stored.</summary>
public static class SecureToken
{
    /// <summary>A longer token cannot be one of ours: reject it early.</summary>
    public const int MaxLength = 100;

    /// <summary>SHA-256 in hex is always this long.</summary>
    public const int HashLength = 64;

    /// <summary>256 random bits, URL-safe.</summary>
    public static (string Token, string Hash) New()
    {
        var token = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(32));

        return (token, Hash(token));
    }

    public static string Hash(string token) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));

    public static bool LooksValid(string? token) => !string.IsNullOrWhiteSpace(token) && token.Length <= MaxLength;
}
