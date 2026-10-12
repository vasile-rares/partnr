namespace ClientPortal.Application.DTOs.Clients;

public sealed record ClientResponse(
    Guid Id,
    string Name,
    string Email,
    string? Company,
    string? Phone,
    string? Notes,
    DateTime CreatedAtUtc,
    DateTime? ArchivedAtUtc);
