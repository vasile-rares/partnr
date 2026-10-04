using ClientPortal.Application.Clients.Dtos.Responses;
using ClientPortal.Domain.Clients;

namespace ClientPortal.Application.Clients;

internal static class ClientMappings
{
    public static ClientResponse ToResponse(this Client client) =>
        new(client.Id, client.Name, client.Email, client.CreatedAtUtc);
}
