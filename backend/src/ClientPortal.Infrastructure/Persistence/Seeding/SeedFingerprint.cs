using System.Reflection;
using System.Security.Cryptography;
using System.Text;

namespace ClientPortal.Infrastructure.Persistence.Seeding;

/// <summary>A hash of the seed's own source, embedded at build time, so editing the seed is detected without bumping a version.</summary>
internal static class SeedFingerprint
{
    private const string ResourcePrefix = "SeedSource/";

    public static string Current { get; } = Compute();

    private static string Compute()
    {
        var assembly = typeof(SeedFingerprint).Assembly;
        var text = new StringBuilder();

        foreach (var name in assembly.GetManifestResourceNames().Where(n => n.StartsWith(ResourcePrefix, StringComparison.Ordinal)).Order())
        {
            using var reader = new StreamReader(assembly.GetManifestResourceStream(name)!);
            // Line endings differ between machines (git may convert them); they are not a change to the data.
            text.Append(name).Append('\n').Append(reader.ReadToEnd().Replace("\r\n", "\n")).Append('\n');
        }

        if (text.Length == 0)
        {
            throw new InvalidOperationException("The seed's source is not embedded in the assembly; see ClientPortal.Infrastructure.csproj.");
        }

        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(text.ToString())))[..16].ToLowerInvariant();
    }
}
