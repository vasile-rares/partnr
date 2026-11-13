using Partnr.Application.Interfaces.Services;
using Partnr.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Partnr.Infrastructure.Authentication;

internal static class IdentityServiceCollectionExtensions
{
    public static IServiceCollection AddIdentityAuthentication(this IServiceCollection services)
    {
        services
            .AddIdentityCore<ApplicationUser>(options =>
            {
                options.User.RequireUniqueEmail = true;

                // Length over composition rules (NIST style).
                options.Password.RequiredLength = 8;
                options.Password.RequireDigit = false;
                options.Password.RequireLowercase = false;
                options.Password.RequireUppercase = false;
                options.Password.RequireNonAlphanumeric = false;

                options.Lockout.MaxFailedAccessAttempts = 5;
                options.Lockout.DefaultLockoutTimeSpan = TimeSpan.FromMinutes(15);
            })
            .AddSignInManager()
            .AddEntityFrameworkStores<ApplicationDbContext>();

        services.AddAuthentication(IdentityConstants.ApplicationScheme).AddIdentityCookies();

        // Identity re-checks the account behind a cookie only every 30 minutes by default. Checking on every request makes a cookie
        // whose account is gone plain anonymous: a clean 401, and the cookie is cleared.
        services.Configure<SecurityStampValidatorOptions>(options => options.ValidationInterval = TimeSpan.Zero);

        services.AddOptions<CookieAuthenticationOptions>(IdentityConstants.ApplicationScheme)
            .Configure<IHostEnvironment>((options, environment) =>
            {
                options.ExpireTimeSpan = TimeSpan.FromDays(14);
                options.SlidingExpiration = true;
                options.Cookie.HttpOnly = true;
                options.Cookie.SameSite = SameSiteMode.Lax;

                // The dev API runs on plain http://localhost; everywhere else the cookie is HTTPS-only.
                options.Cookie.SecurePolicy = environment.IsDevelopment()
                    ? CookieSecurePolicy.SameAsRequest
                    : CookieSecurePolicy.Always;

                // The security stamp validator runs on every request (see above) and asks for the cookie to be re-issued each time.
                // A request still in flight when someone signs out would then answer after the sign-out with a fresh cookie, and the
                // browser would keep it: signed in again. So renewal stays what the cookie handler decided from the time left.
                var validatePrincipal = options.Events.OnValidatePrincipal;
                options.Events.OnValidatePrincipal = async context =>
                {
                    var renewalDue = context.ShouldRenew;
                    await validatePrincipal(context);
                    context.ShouldRenew = renewalDue;
                };

                // This is an API: answer 401 / 403 instead of redirecting to a login page.
                options.Events.OnRedirectToLogin = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                    return Task.CompletedTask;
                };
                options.Events.OnRedirectToAccessDenied = context =>
                {
                    context.Response.StatusCode = StatusCodes.Status403Forbidden;
                    return Task.CompletedTask;
                };
            });

        services.AddScoped<IIdentityService, IdentityService>();

        return services;
    }
}
