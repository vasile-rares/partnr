namespace ClientPortal.Application.DTOs.Clients;

/// <summary>An existing account whose email starts with what the user has typed, offered while adding a client.</summary>
/// <param name="AlreadyClient">The workspace already has a client card with this email (linked or not).</param>
public sealed record ClientSuggestionResponse(string Email, string FullName, bool AlreadyClient);
