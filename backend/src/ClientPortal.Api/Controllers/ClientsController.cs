using ClientPortal.Application.DTOs.Clients;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/clients")]
public sealed class ClientsController(IClientService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Register(Guid workspaceId, RegisterClientRequest request, CancellationToken cancellationToken)
    {
        var client = await service.RegisterAsync(workspaceId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { workspaceId, id = client.Id }, client);
    }

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(workspaceId, id, cancellationToken));
}
