using System.ComponentModel.DataAnnotations;

namespace Partnr.Integrations.Application.DTOs.Google;

/// <summary><c>Available</c> is false when the server has no Google credentials; <c>Status</c> is NotConnected, Connected or NeedsReconnect.</summary>
public sealed record GoogleConnectionResponse(bool Available, string Status, string? Email);

public sealed record GoogleConnectUrlResponse(string Url);

/// <summary>Where in the web app the browser comes back to after Google (a path of the app, no query); without it, the account page.</summary>
public sealed record GoogleConnectRequest
{
    [MaxLength(200)]
    [RegularExpression("^/[A-Za-z0-9/_-]*$", ErrorMessage = "ReturnTo must be a path of the web app.")]
    public string? ReturnTo { get; init; }
}

/// <summary>How the return from Google ended; the callback turns it into a page of the web app.</summary>
public enum GoogleConnectOutcome
{
    Connected,
    Denied,
    MissingPermission,
    Failed,
}
