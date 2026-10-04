namespace ClientPortal.Application.Clients.Dtos.Responses;

public sealed record ClientResponse(Guid Id, string Name, string Email, DateTime CreatedAtUtc);
