namespace Partnr.Storage.Application;

/// <summary>The one place that says how much a file may weigh, whoever asks.</summary>
public static class StorageLimits
{
    public const long MaxFileBytes = 25 * 1024 * 1024;
}
