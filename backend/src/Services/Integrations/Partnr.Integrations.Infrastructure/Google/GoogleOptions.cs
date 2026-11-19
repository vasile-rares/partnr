using System.ComponentModel.DataAnnotations;
using System.Security.Cryptography;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Options;

namespace Partnr.Integrations.Infrastructure.Google;

/// <summary>The "Google" section. Without a <see cref="ClientId"/> the Google features are simply off.</summary>
public sealed class GoogleOptions
{
    public const string SectionName = "Google";

    /// <summary>AES-256 needs exactly this many bytes.</summary>
    public const int KeyLength = 32;

    public string ClientId { get; init; } = string.Empty;

    public string ClientSecret { get; init; } = string.Empty;

    /// <summary>Where Google sends the browser back; it must match the Google Cloud console exactly and go through the web app's /api rewrite.</summary>
    public string RedirectUri { get; init; } = string.Empty;

    /// <summary>Base64 of 32 random bytes; protects the saved Google tokens. Losing it means everyone connects again.</summary>
    public string EncryptionKey { get; init; } = string.Empty;

    /// <summary>Off in the integration tests, which call the sync service themselves so they stay deterministic.</summary>
    public bool SyncWorkerEnabled { get; init; } = true;

    [Range(1, 3600)]
    public int SyncPollSeconds { get; init; } = 5;

    /// <summary>Replaces Google with a stand-in for the browser tests. Refused outside Development.</summary>
    public bool Fake { get; init; }

    public bool IsConfigured => Fake || (!string.IsNullOrWhiteSpace(ClientId) && !string.IsNullOrWhiteSpace(ClientSecret));

    public static bool TryReadKey(string value, out byte[] key)
    {
        key = [];
        Span<byte> buffer = stackalloc byte[KeyLength + 3];
        if (!Convert.TryFromBase64String(value, buffer, out var written) || written != KeyLength)
        {
            return false;
        }

        key = buffer[..written].ToArray();
        return true;
    }

    public static string NewKey() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(KeyLength));
}

internal sealed class GoogleOptionsValidator(IHostEnvironment environment) : IValidateOptions<GoogleOptions>
{
    public ValidateOptionsResult Validate(string? name, GoogleOptions options)
    {
        // A fake Google in production would accept anyone's "consent" and keep no real calendar.
        if (options.Fake && !environment.IsDevelopment())
        {
            return ValidateOptionsResult.Fail("Google:Fake is only allowed in the Development environment.");
        }

        if (!options.IsConfigured)
        {
            return ValidateOptionsResult.Success;
        }
        if (!Uri.TryCreate(options.RedirectUri, UriKind.Absolute, out _))
        {
            return ValidateOptionsResult.Fail("Google:RedirectUri must be an absolute address.");
        }

        return GoogleOptions.TryReadKey(options.EncryptionKey, out _)
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail($"Google:EncryptionKey must be base64 of {GoogleOptions.KeyLength} bytes (make one with: openssl rand -base64 32).");
    }
}
