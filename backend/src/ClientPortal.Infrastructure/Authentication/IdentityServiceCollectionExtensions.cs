using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Infrastructure.Persistence;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClientPortal.Infrastructure.Authentication;

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

        // Identity re-checks the account behind a cookie only every 30 minutes by default. Until then a cookie whose account is
        // gone (deleted, or the development database was reseeded) still counts as signed in and reaches the controllers, which
        // then fail in odd places. Checking on every request makes such a cookie plain anonymous: a clean 401, and the cookie is cleared.
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
