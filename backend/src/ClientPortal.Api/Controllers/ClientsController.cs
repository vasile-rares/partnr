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

    [HttpGet]
    // includeArchived is false when the query string leaves it out.
    public async Task<IActionResult> List(Guid workspaceId, [FromQuery] bool includeArchived, CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, includeArchived, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(workspaceId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid workspaceId, Guid id, UpdateClientRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(workspaceId, id, request, cancellationToken));

    // Archive / restore are actions, not field edits, so they get their own endpoints instead of a flag in PUT.
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ArchiveAsync(workspaceId, id, cancellationToken));

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RestoreAsync(workspaceId, id, cancellationToken));
}
