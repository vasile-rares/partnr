using System.Buffers.Text;
using System.Text.Json;
using Partnr.Application.Interfaces.Services;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Partnr.Infrastructure.Google;

internal sealed class GoogleOAuthClient(HttpClient http, IOptions<GoogleOptions> options, ILogger<GoogleOAuthClient> logger)
    : IGoogleOAuthClient
{
    public const string CalendarScope = "https://www.googleapis.com/auth/calendar.events";

    private const string AuthorizeEndpoint = "https://accounts.google.com/o/oauth2/v2/auth";
    private const string TokenEndpoint = "https://oauth2.googleapis.com/token";
    private const string RevokeEndpoint = "https://oauth2.googleapis.com/revoke";

    private GoogleOptions Settings => options.Value;

    public bool IsConfigured => Settings.IsConfigured;

    public string AuthorizationUrl(string state)
    {
        var query = new (string Key, string Value)[]
        {
            ("client_id", Settings.ClientId),
            ("redirect_uri", Settings.RedirectUri),
            ("response_type", "code"),
            // "openid email" only to learn which Google account it is; the calendar is the one real permission.
            ("scope", $"openid email {CalendarScope}"),
            // Offline access and a forced consent screen are what make Google hand over a refresh token every time.
            ("access_type", "offline"),
            ("prompt", "consent"),
            ("state", state),
        };

        return $"{AuthorizeEndpoint}?{string.Join('&', query.Select(p => $"{p.Key}={Uri.EscapeDataString(p.Value)}"))}";
    }

    public async Task<GoogleGrant?> ExchangeCodeAsync(string code, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsync(TokenEndpoint, new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["code"] = code,
                ["client_id"] = Settings.ClientId,
                ["client_secret"] = Settings.ClientSecret,
                ["redirect_uri"] = Settings.RedirectUri,
                ["grant_type"] = "authorization_code",
            }), cancellationToken);

            var body = await response.Content.ReadAsStringAsync(cancellationToken);
            using var json = JsonDocument.Parse(body);
            var root = json.RootElement;

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google refused the code: {Status} {Error}", (int)response.StatusCode, Text(root, "error"));
                return null;
            }

            var scopes = (Text(root, "scope") ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);

            return new GoogleGrant(Text(root, "refresh_token"), EmailIn(Text(root, "id_token")), scopes.Contains(CalendarScope));
        }
        catch (Exception ex) when (ex is HttpRequestException or JsonException or FormatException or TaskCanceledException
                                       && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not exchange a Google code");
            return null;
        }
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken cancellationToken)
    {
        try
        {
            using var response = await http.PostAsync(
                RevokeEndpoint,
                new FormUrlEncodedContent(new Dictionary<string, string> { ["token"] = refreshToken }),
                cancellationToken);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogWarning("Google did not revoke a token: {Status}", (int)response.StatusCode);
            }
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            logger.LogWarning(ex, "Could not revoke a Google token");
        }
    }

    private static string? Text(JsonElement element, string name) =>
        element.ValueKind == JsonValueKind.Object && element.TryGetProperty(name, out var value) && value.ValueKind == JsonValueKind.String
            ? value.GetString()
            : null;

    // The id_token came straight from Google over TLS in answer to our own request, so its payload is read without checking the signature.
    private static string? EmailIn(string? idToken)
    {
        var parts = idToken?.Split('.');
        if (parts is not [_, var payload, _])
        {
            return null;
        }

        using var json = JsonDocument.Parse(Base64Url.DecodeFromChars(payload));

        return Text(json.RootElement, "email");
    }
}
