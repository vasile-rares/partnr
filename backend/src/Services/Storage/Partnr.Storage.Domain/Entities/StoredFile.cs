using System.Text.RegularExpressions;
using Partnr.Storage.Domain.Exceptions;

namespace Partnr.Storage.Domain.Entities;

/// <summary>What is known about the bytes under one key. The bytes themselves are in the object store.</summary>
public sealed partial class StoredFile : Entity
{
    public const int MaxKeyLength = 512;

    private StoredFile()
    {
    }

    public string Key { get; private set; } = string.Empty;

    public long SizeBytes { get; private set; }

    public string ContentType { get; private set; } = string.Empty;

    /// <summary>Lowercase hex SHA-256 of the bytes.</summary>
    public string Sha256 { get; private set; } = string.Empty;

    public DateTime CreatedAtUtc { get; private set; }

    public DateTime UpdatedAtUtc { get; private set; }

    public static StoredFile Create(string key, long sizeBytes, string contentType, string sha256, DateTime nowUtc)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > MaxKeyLength)
        {
            throw new DomainException($"A key is required and at most {MaxKeyLength} characters.");
        }

        var file = new StoredFile { Key = key, CreatedAtUtc = nowUtc };
        file.Replace(sizeBytes, contentType, sha256, nowUtc);

        return file;
    }

    /// <summary>Saving under an existing key replaces the bytes, so the record follows.</summary>
    public void Replace(long sizeBytes, string contentType, string sha256, DateTime nowUtc)
    {
        if (sizeBytes < 0)
        {
            throw new DomainException("A size cannot be negative.");
        }

        if (!Sha256Format().IsMatch(sha256))
        {
            throw new DomainException("The checksum must be a SHA-256 in lowercase hex.");
        }

        SizeBytes = sizeBytes;
        ContentType = string.IsNullOrWhiteSpace(contentType) ? "application/octet-stream" : contentType.Trim();
        Sha256 = sha256;
        UpdatedAtUtc = nowUtc;
    }

    [GeneratedRegex("^[0-9a-f]{64}$")]
    private static partial Regex Sha256Format();
}
