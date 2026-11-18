namespace Partnr.Gateway.Sessions;

internal interface ISessionValidator
{
    Task<SessionCheck> ValidateAsync(string cookieHeader, CancellationToken cancellationToken);
}

internal enum SessionOutcome
{
    Valid,
    Invalid,

    /// <summary>The service that owns the sessions could not be asked; the visitor may well be signed in.</summary>
    Unavailable,
}

/// <param name="SetCookies">What the session check wants the browser to get: a renewed cookie, or the removal of one that no longer counts.</param>
internal sealed record SessionCheck(SessionOutcome Outcome, string? UserId = null, IReadOnlyList<string>? SetCookies = null);
