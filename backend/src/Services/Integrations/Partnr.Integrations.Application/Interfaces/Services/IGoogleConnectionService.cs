using Partnr.Integrations.Application.DTOs.Google;

namespace Partnr.Integrations.Application.Interfaces.Services;

/// <summary>The signed-in account's own link to Google Calendar (no workspace: the permission belongs to the person).</summary>
public interface IGoogleConnectionService
{
    Task<GoogleConnectionResponse> GetAsync(CancellationToken cancellationToken);

    /// <summary>The address of Google's consent page. <paramref name="returnTo"/> is a path of the web app to come back to; it travels inside the protected state.</summary>
    Task<GoogleConnectUrlResponse> StartAsync(string? returnTo, CancellationToken cancellationToken);

    /// <summary>Handles the browser coming back from Google. Expected failures are an outcome, not an exception, since the person sees a page, not JSON.</summary>
    Task<GoogleConnectOutcome> CompleteAsync(string? code, string? state, string? error, CancellationToken cancellationToken);

    /// <summary>The path <see cref="StartAsync"/> was given, read back from the state Google returns; null when there is none or the state is not ours.</summary>
    string? ReturnPathOf(string? state);

    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>NotConnected, Connected or NeedsReconnect, for any account: asked by other services (over gRPC), not by the person.</summary>
    Task<string> StatusOfAsync(Guid userId, CancellationToken cancellationToken);
}
