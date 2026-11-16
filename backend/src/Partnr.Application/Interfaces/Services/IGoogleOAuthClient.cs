namespace Partnr.Application.Interfaces.Services;

/// <summary>What Google gave back for a code. <see cref="RefreshToken"/> is null when Google sent none.</summary>
public sealed record GoogleGrant(string? RefreshToken, string? Email, bool CanUseCalendar);

/// <summary>Google's sign-in and permission screens, as far as the app needs them.</summary>
public interface IGoogleOAuthClient
{
    /// <summary>False when the server has no Google credentials: the feature is then off.</summary>
    bool IsConfigured { get; }

    /// <summary>The Google page where the person grants access; Google sends them back with the same <paramref name="state"/>.</summary>
    string AuthorizationUrl(string state);

    /// <summary>Null when Google refused the code or could not be reached.</summary>
    Task<GoogleGrant?> ExchangeCodeAsync(string code, CancellationToken cancellationToken);

    /// <summary>Best effort: it never throws, so a connection can always be removed on our side.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken cancellationToken);
}
