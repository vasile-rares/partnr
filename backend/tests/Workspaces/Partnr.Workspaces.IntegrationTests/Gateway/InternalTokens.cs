using System.Security.Claims;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Partnr.Contracts;

namespace Partnr.Workspaces.IntegrationTests.Gateway;

/// <summary>Makes the JWT the gateway would, with the key the test API is configured with (or another one, to try a forgery).</summary>
internal static class InternalTokens
{
    // 32 bytes, base64; only ever used by these tests.
    public const string SigningKey = "aW50ZWdyYXRpb24tdGVzdC1zaWduaW5nLWtleS0zMmIh";

    public static string Issue(
        string userId,
        string signingKey = SigningKey,
        string issuer = InternalAuth.Issuer,
        string audience = InternalAuth.Audience,
        TimeSpan? lifetime = null)
    {
        var now = DateTime.UtcNow;
        var expires = now.Add(lifetime ?? InternalAuth.TokenLifetime);

        return new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = issuer,
            Audience = audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId)]),
            IssuedAt = now.AddMinutes(-10),
            NotBefore = now.AddMinutes(-10),
            Expires = expires,
            SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(Convert.FromBase64String(signingKey)), SecurityAlgorithms.HmacSha256),
        });
    }
}
