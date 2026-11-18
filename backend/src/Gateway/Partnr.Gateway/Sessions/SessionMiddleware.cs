using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Primitives;
using Microsoft.Net.Http.Headers;

namespace Partnr.Gateway.Sessions;

/// <summary>
/// Turns the browser's session cookie into the JWT the services understand. A request without a (valid) session goes on without one:
/// the service answers 401 or serves it, depending on whether the route is open to anyone.
/// </summary>
internal sealed class SessionMiddleware(RequestDelegate next, ISessionValidator sessions, InternalTokenIssuer tokens, IConfiguration configuration)
{
    private readonly string _cookieName = configuration["Session:CookieName"] ?? ".AspNetCore.Identity.Application";

    public async Task InvokeAsync(HttpContext context)
    {
        // Whatever a caller sends as Authorization is theirs, never ours: only the token made below reaches the services.
        context.Request.Headers.Remove(HeaderNames.Authorization);

        // The cookie can come in pieces (.AspNetCore.Identity.Application, ...C1, ...C2) when it is large.
        if (!context.Request.Cookies.Keys.Any(name => name.StartsWith(_cookieName, StringComparison.Ordinal)))
        {
            await next(context);
            return;
        }

        var check = await sessions.ValidateAsync(string.Join("; ", context.Request.Headers.Cookie.ToArray()), context.RequestAborted);

        if (check.Outcome == SessionOutcome.Unavailable)
        {
            // Not "signed out": the visitor may well have a good session, the gateway just cannot tell right now.
            await Results.Problem(statusCode: StatusCodes.Status503ServiceUnavailable, title: "The service is not available right now.")
                .ExecuteAsync(context);
            return;
        }

        if (check.SetCookies is { Count: > 0 } setCookies)
        {
            context.Response.OnStarting(() =>
            {
                // First, so a cookie the service sets itself in the same answer (a sign-out, a new sign-in) is the one the browser keeps.
                context.Response.Headers.SetCookie = new StringValues([.. setCookies, .. context.Response.Headers.SetCookie.ToArray()]);
                return Task.CompletedTask;
            });
        }

        if (check is { Outcome: SessionOutcome.Valid, UserId: { } userId })
        {
            context.Request.Headers.Authorization = $"Bearer {tokens.Issue(userId)}";
        }

        await next(context);
    }
}
