using ClientPortal.Api.Extensions;
using ClientPortal.Application.Common.Messaging;
using ClientPortal.Application.Features.Clients.Commands.RegisterClient;
using ClientPortal.Application.Features.Clients.Queries.GetClientById;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(
        RegisterClientCommand command,
        [FromServices] ICommandHandler<RegisterClientCommand, Guid> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(command, cancellationToken);

        return result.IsSuccess
            ? CreatedAtAction(nameof(GetById), new { id = result.Value }, new { id = result.Value })
            : result.ToProblemResult();
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(
        Guid id,
        [FromServices] IQueryHandler<GetClientByIdQuery, ClientDto> handler,
        CancellationToken cancellationToken)
    {
        var result = await handler.HandleAsync(new GetClientByIdQuery(id), cancellationToken);

        return result.IsSuccess ? Ok(result.Value) : result.ToProblemResult();
    }
}
