namespace ClientPortal.Application.DTOs.Clients;

/// <param name="AlreadyClient">The workspace already has a client card with this email.</param>
public sealed record ClientSuggestionResponse(string Email, string FullName, bool AlreadyClient);
