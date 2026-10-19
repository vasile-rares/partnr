using System.ComponentModel.DataAnnotations;

namespace ClientPortal.Infrastructure.Storage;

/// <summary>The "Storage" configuration section: any S3-compatible service. Validated at startup, like <c>EmailOptions</c>.</summary>
public sealed class StorageOptions
{
    public const string SectionName = "Storage";

    /// <summary>Leave empty for AWS S3 itself; set it for R2, B2 or a local RustFS.</summary>
    public string? ServiceUrl { get; init; }

    [Required]
    public string Region { get; init; } = "us-east-1";

    [Required]
    public string Bucket { get; init; } = string.Empty;

    [Required]
    public string AccessKey { get; init; } = string.Empty;

    [Required]
    public string SecretKey { get; init; } = string.Empty;

    public bool ForcePathStyle { get; init; } = true;

    /// <summary>Development and tests only: production buckets are made ahead, with credentials that cannot create them.</summary>
    public bool CreateBucketIfMissing { get; init; }
}
