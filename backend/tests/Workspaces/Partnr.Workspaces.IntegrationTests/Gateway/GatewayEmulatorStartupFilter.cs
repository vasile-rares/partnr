using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Net.Http.Headers;

namespace Partnr.Workspaces.IntegrationTests.Gateway;

/// <summary>
/// Plays the gateway in front of the test API: a request that carries a good session cookie gets the JWT the API now asks for,
/// so the tests can keep signing in with cookies as the browser does. The cookie is checked by the same handler the real session
/// check uses (see <c>SessionValidationTests</c>), in the same request, so a rejected cookie is cleared on this very answer.
/// </summary>
internal sealed class GatewayEmulatorStartupFilter : IStartupFilter
{
    public Action<IApplicationBuilder> Configure(Action<IApplicationBuilder> next) => app =>
    {
        app.Use(async (context, nextMiddleware) =>
        {
            context.Request.Headers.Remove(HeaderNames.Authorization);

            if (context.Request.Cookies.Keys.Any(name => name.StartsWith(".AspNetCore.Identity.Application", StringComparison.Ordinal)))
            {
                var result = await context.AuthenticateAsync(IdentityConstants.ApplicationScheme);

                if (result.Principal?.FindFirstValue(ClaimTypes.NameIdentifier) is { } userId && result.Succeeded)
                {
                    context.Request.Headers.Authorization = $"Bearer {InternalTokens.Issue(userId)}";
                }
            }

            await nextMiddleware(context);
        });

        next(app);
    };
}
