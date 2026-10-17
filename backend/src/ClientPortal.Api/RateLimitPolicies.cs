namespace ClientPortal.Api;

internal static class RateLimitPolicies
{
    /// <summary>Looking up accounts while typing. Per signed-in user.</summary>
    public const string Suggestions = "suggestions";

    public const int SuggestionsPerMinute = 60;
}
