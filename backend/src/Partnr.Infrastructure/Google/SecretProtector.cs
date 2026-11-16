using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;
using Partnr.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Partnr.Infrastructure.Google;

/// <summary>AES-256-GCM with a key from the configuration, not ASP.NET's key ring: that one is lost when a container restarts, and every saved Google token would go with it.</summary>
internal sealed class SecretProtector(IOptions<GoogleOptions> options) : ISecretProtector
{
    private const int NonceLength = 12;
    private const int TagLength = 16;

    public string Protect(string purpose, string plaintext)
    {
        var plain = Encoding.UTF8.GetBytes(plaintext);
        var nonce = RandomNumberGenerator.GetBytes(NonceLength);
        var tag = new byte[TagLength];
        var cipher = new byte[plain.Length];

        using var aes = new AesGcm(Key(), TagLength);
        // The purpose is authenticated, not stored: a value made for one purpose does not open under another.
        aes.Encrypt(nonce, plain, cipher, tag, Encoding.UTF8.GetBytes(purpose));

        return Base64Url.EncodeToString([.. nonce, .. tag, .. cipher]);
    }

    public string? Unprotect(string purpose, string protectedValue)
    {
        try
        {
            var bytes = Base64Url.DecodeFromChars(protectedValue);
            if (bytes.Length < NonceLength + TagLength)
            {
                return null;
            }

            var cipher = bytes.AsSpan(NonceLength + TagLength);
            var plain = new byte[cipher.Length];

            using var aes = new AesGcm(Key(), TagLength);
            aes.Decrypt(bytes.AsSpan(0, NonceLength), cipher, bytes.AsSpan(NonceLength, TagLength), plain, Encoding.UTF8.GetBytes(purpose));

            return Encoding.UTF8.GetString(plain);
        }
        catch (Exception ex) when (ex is FormatException or CryptographicException)
        {
            return null;
        }
    }

    private byte[] Key() =>
        GoogleOptions.TryReadKey(options.Value.EncryptionKey, out var key)
            ? key
            : throw new InvalidOperationException("Google:EncryptionKey is not set.");
}
