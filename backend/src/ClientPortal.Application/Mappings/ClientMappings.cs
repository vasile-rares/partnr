using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Domain.Entities;

namespace ClientPortal.Application.Mappings;

internal static class ClientMappings
{
    public static ClientResponse ToResponse(this Client client) =>
        new(client.Id, client.Name, client.Email, client.CreatedAtUtc);
}
