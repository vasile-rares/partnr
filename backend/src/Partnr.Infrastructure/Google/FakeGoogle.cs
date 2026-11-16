using Partnr.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Partnr.Infrastructure.Google;

// A stand-in for Google, switched on by `Google:Fake` (Development only, see GoogleOptionsValidator). It lets the browser tests
// walk the whole "Connect Google" and "schedule a meeting" flow without an account or a network.

/// <summary>
/// The consent screen is skipped: the browser is sent straight back to the callback with a code. A code of
/// <see cref="NoCalendarCode"/> stands for someone who unticked the calendar on Google's screen.
/// </summary>
internal sealed class FakeGoogleOAuthClient(IOptions<GoogleOptions> options) : IGoogleOAuthClient
{
    public const string OkCode = "fake-ok";
    public const string NoCalendarCode = "fake-no-calendar";

    public bool IsConfigured => true;

    public string AuthorizationUrl(string state) =>
        $"{options.Value.RedirectUri}?code={OkCode}&state={Uri.EscapeDataString(state)}";

    public Task<GoogleGrant?> ExchangeCodeAsync(string code, CancellationToken cancellationToken) =>
        Task.FromResult<GoogleGrant?>(code is OkCode or NoCalendarCode
            ? new GoogleGrant($"fake-refresh-{Guid.NewGuid():N}", "fake.user@gmail.example", code == OkCode)
            : null);

    public Task RevokeAsync(string refreshToken, CancellationToken cancellationToken) => Task.CompletedTask;
}

/// <summary>Every event works and gets a Meet link, except for a meeting whose title contains <see cref="FailureMarker"/>: Google is "down" for it.</summary>
internal sealed class FakeGoogleCalendarClient : IGoogleCalendarClient
{
    public const string FailureMarker = "[google-down]";

    public Task<GoogleEventResult> UpsertEventAsync(string refreshToken, string eventId, bool exists, GoogleEventDetails details, CancellationToken cancellationToken) =>
        details.Title.Contains(FailureMarker, StringComparison.Ordinal)
            ? throw new GoogleCalendarException("Google is down (fake).")
            : Task.FromResult(new GoogleEventResult(eventId, $"https://meet.google.com/{eventId[..3]}-{eventId[3..7]}-{eventId[7..10]}"));

    public Task DeleteEventAsync(string refreshToken, string eventId, CancellationToken cancellationToken) => Task.CompletedTask;
}
