namespace ClientPortal.Api;

/// <summary>Names of the rate limiting policies, shared by Program.cs (which defines them) and the controllers (which opt in).</summary>
internal static class RateLimitPolicies
{
    /// <summary>Looking up accounts while typing. Per signed-in user, so one user cannot use up the allowance of another.</summary>
    public const string Suggestions = "suggestions";

    public const int SuggestionsPerMinute = 60;
}
