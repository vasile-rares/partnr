using ClientPortal.Application.Common.Messaging;
using ClientPortal.Domain.Errors;
using ClientPortal.Domain.Interfaces;
using ClientPortal.SharedKernel.Results;

namespace ClientPortal.Application.Features.Clients.Queries.GetClientById;

public sealed class GetClientByIdHandler(IClientRepository clients) : IQueryHandler<GetClientByIdQuery, ClientDto>
{
    public async Task<Result<ClientDto>> HandleAsync(GetClientByIdQuery query, CancellationToken cancellationToken)
    {
        var client = await clients.GetByIdAsync(query.Id, cancellationToken);
        if (client is null)
        {
            return ClientErrors.NotFound(query.Id);
        }

        return new ClientDto(client.Id, client.Name, client.Email, client.CreatedAtUtc);
    }
}
