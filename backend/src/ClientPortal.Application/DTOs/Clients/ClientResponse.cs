namespace ClientPortal.Application.DTOs.Clients;

public sealed record ClientResponse(Guid Id, string Name, string Email, DateTime CreatedAtUtc);
