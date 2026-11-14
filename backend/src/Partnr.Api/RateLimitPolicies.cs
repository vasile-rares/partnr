namespace Partnr.Api;

internal static class RateLimitPolicies
{
    /// <summary>Looking up accounts while typing. Per signed-in user.</summary>
    public const string Suggestions = "suggestions";

    public const int SuggestionsPerMinute = 60;

    /// <summary>
    /// "Forgot password" for everyone together: behind the web app's proxy the API sees one address for every visitor, so a
    /// per-address limit would be a global one anyway. It protects the mail server; each account is also limited to one email a minute.
    /// </summary>
    public const string PasswordReset = "password-reset";

    public const int PasswordResetsPerMinute = 30;
}
