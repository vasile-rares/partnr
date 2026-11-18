using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Partnr.Contracts;

namespace Partnr.ServiceDefaults;

public static class InternalAuthExtensions
{
    /// <summary>The signing key of the internal JWT, bound and checked at startup. The gateway needs only this part: it makes the tokens.</summary>
    public static IServiceCollection AddInternalAuthOptions(this IServiceCollection services)
    {
        services.AddOptions<InternalAuthOptions>()
            .BindConfiguration(InternalAuth.ConfigurationSection)
            .ValidateDataAnnotations()
            .Validate(InternalAuthOptions.IsValidKey, $"InternalAuth:SigningKey must be base64 and at least {InternalAuthOptions.MinimumKeyBytes} bytes.")
            .ValidateOnStart();

        return services;
    }

    /// <summary>
    /// Makes the gateway's JWT the way a request proves who it is, and JwtBearer the default scheme. Call it after anything that
    /// registers another default scheme (the API's Identity cookie), so the JWT is the one that wins.
    /// </summary>
    public static IServiceCollection AddInternalAuthentication(this IServiceCollection services)
    {
        services.AddInternalAuthOptions();

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
