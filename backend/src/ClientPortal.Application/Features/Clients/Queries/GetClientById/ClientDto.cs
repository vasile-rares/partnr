namespace ClientPortal.Application.Features.Clients.Queries.GetClientById;

public sealed record ClientDto(Guid Id, string Name, string Email, DateTime CreatedAtUtc);
