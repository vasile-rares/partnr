using Partnr.Integrations.Application.Interfaces.Services;
using Microsoft.Extensions.Options;

namespace Partnr.Integrations.Infrastructure.Google;

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
