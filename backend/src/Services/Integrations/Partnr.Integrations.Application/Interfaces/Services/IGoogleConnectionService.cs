using Partnr.Integrations.Application.DTOs.Google;

namespace Partnr.Integrations.Application.Interfaces.Services;

/// <summary>The signed-in account's own link to Google Calendar (no workspace: the permission belongs to the person).</summary>
public interface IGoogleConnectionService
{
    Task<GoogleConnectionResponse> GetAsync(CancellationToken cancellationToken);

    Task<GoogleConnectUrlResponse> StartAsync(CancellationToken cancellationToken);

    /// <summary>Handles the browser coming back from Google. Expected failures are an outcome, not an exception, since the person sees a page, not JSON.</summary>
    Task<GoogleConnectOutcome> CompleteAsync(string? code, string? state, string? error, CancellationToken cancellationToken);

    Task DisconnectAsync(CancellationToken cancellationToken);

    /// <summary>NotConnected, Connected or NeedsReconnect, for any account: asked by other services (over gRPC), not by the person.</summary>
    Task<string> StatusOfAsync(Guid userId, CancellationToken cancellationToken);
}
