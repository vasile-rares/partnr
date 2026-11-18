using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Partnr.Contracts;

namespace Partnr.Workspaces.Api.Authentication;

internal static class InternalAuthExtensions
{
    /// <summary>
    /// Makes the gateway's JWT the way a request proves who it is. The session cookie is no longer read on the API routes: it only
    /// lives on in the sign-in routes (they set and clear it) and in the session check the gateway asks for. Call it after
    /// AddInfrastructure, so the JWT, not the cookie, is the default scheme.
    /// </summary>
    public static IServiceCollection AddInternalAuthentication(this IServiceCollection services)
    {
        services.AddOptions<InternalAuthOptions>()
            .BindConfiguration(InternalAuth.ConfigurationSection)
            .ValidateDataAnnotations()
            .Validate(InternalAuthOptions.IsValidKey, $"InternalAuth:SigningKey must be base64 and at least {InternalAuthOptions.MinimumKeyBytes} bytes.")
            .ValidateOnStart();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme).AddJwtBearer();

        services.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IOptions<InternalAuthOptions>>((jwt, auth) =>
            {
                jwt.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidIssuer = InternalAuth.Issuer,
                    ValidAudience = InternalAuth.Audience,
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    IssuerSigningKey = new SymmetricSecurityKey(auth.Value.KeyBytes()),
                    RequireExpirationTime = true,
                    // The token lives one minute; the default five minutes of tolerance would be five times its life.
                    ClockSkew = TimeSpan.FromSeconds(10),
                };
            });

        return services;
    }
}
