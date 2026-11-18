namespace Partnr.Contracts;

/// <summary>What the gateway and the services agree on about the short JWT the gateway puts on every request it forwards.</summary>
public static class InternalAuth
{
    public const string Issuer = "partnr-gateway";
    public const string Audience = "partnr-services";

    /// <summary>The configuration section holding <c>SigningKey</c>, a base64 string of at least 32 bytes shared by the gateway and the services.</summary>
    public const string ConfigurationSection = "InternalAuth";

    public static readonly TimeSpan TokenLifetime = TimeSpan.FromMinutes(1);
}
