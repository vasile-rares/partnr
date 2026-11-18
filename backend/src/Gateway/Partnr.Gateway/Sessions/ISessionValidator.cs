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

internal readonly record struct SessionCheck(SessionOutcome Outcome, string? UserId = null);
