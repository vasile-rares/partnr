using ClientPortal.Application.Clients;
using ClientPortal.Application.Clients.Dtos.Requests;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/clients")]
public sealed class ClientsController(IClientService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(RegisterClientRequest request, CancellationToken cancellationToken)
    {
        var client = await service.RegisterAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = client.Id }, client);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(id, cancellationToken));
}
