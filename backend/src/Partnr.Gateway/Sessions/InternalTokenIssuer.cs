using System.Security.Claims;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Partnr.Contracts;

namespace Partnr.Gateway.Sessions;

/// <summary>Makes the short JWT that tells a service who is calling. Only the gateway can: the services only hold the key to check it.</summary>
internal sealed class InternalTokenIssuer(IOptions<InternalAuthOptions> options, TimeProvider time)
{
    private readonly SigningCredentials _credentials = new(new SymmetricSecurityKey(options.Value.KeyBytes()), SecurityAlgorithms.HmacSha256);
    private readonly JsonWebTokenHandler _handler = new();

    public string Issue(string userId)
    {
        var now = time.GetUtcNow().UtcDateTime;

        return _handler.CreateToken(new SecurityTokenDescriptor
        {
            Issuer = InternalAuth.Issuer,
            Audience = InternalAuth.Audience,
            Subject = new ClaimsIdentity([new Claim(JwtRegisteredClaimNames.Sub, userId)]),
            IssuedAt = now,
            NotBefore = now,
            Expires = now.Add(InternalAuth.TokenLifetime),
            SigningCredentials = _credentials,
        });
    }
}
