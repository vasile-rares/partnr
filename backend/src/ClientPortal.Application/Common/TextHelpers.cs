namespace ClientPortal.Application.Common;

internal static class TextHelpers
{
    /// <summary>A blank optional detail means "not set": stored and returned as null, never as an empty string.</summary>
    public static string? Optional(string? value) => string.IsNullOrWhiteSpace(value) ? null : value.Trim();
}
