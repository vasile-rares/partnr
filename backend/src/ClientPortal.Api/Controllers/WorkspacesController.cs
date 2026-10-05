using ClientPortal.Application.DTOs.Workspaces;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/workspaces")]
public sealed class WorkspacesController(IWorkspaceService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(CreateWorkspaceRequest request, CancellationToken cancellationToken)
    {
        var workspace = await service.CreateAsync(request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { id = workspace.Id }, workspace);
    }

    [HttpGet]
    public async Task<IActionResult> ListMine(CancellationToken cancellationToken) =>
        Ok(await service.ListMineAsync(cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetAsync(id, cancellationToken));

    [HttpPatch("{id:guid}")]
    public async Task<IActionResult> Rename(Guid id, RenameWorkspaceRequest request, CancellationToken cancellationToken) =>
        Ok(await service.RenameAsync(id, request, cancellationToken));
}
