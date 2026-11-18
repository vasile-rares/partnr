namespace Partnr.Workspaces.Application.DTOs.Google;

/// <summary><c>Available</c> is false when the server has no Google credentials; <c>Status</c> is NotConnected, Connected or NeedsReconnect.</summary>
public sealed record GoogleConnectionResponse(bool Available, string Status, string? Email);

public sealed record GoogleConnectUrlResponse(string Url);

/// <summary>How the return from Google ended; the callback turns it into a page of the web app.</summary>
public enum GoogleConnectOutcome
{
    Connected,
    Denied,
    MissingPermission,
    Failed,
}
