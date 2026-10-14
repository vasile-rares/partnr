using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

/// <summary>What a client sees. Everything here is about the signed-in account itself, so there is no workspace in the route.</summary>
[ApiController]
[Route("api/portal")]
public sealed class PortalController(IPortalService service) : ControllerBase
{
    [HttpGet("workspaces")]
    public async Task<IActionResult> ListWorkspaces(CancellationToken cancellationToken) =>
        Ok(await service.ListWorkspacesAsync(cancellationToken));

    [HttpGet("projects")]
    public async Task<IActionResult> ListProjects(CancellationToken cancellationToken) =>
        Ok(await service.ListProjectsAsync(cancellationToken));

    [HttpGet("projects/{id:guid}")]
    public async Task<IActionResult> GetProject(Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetProjectAsync(id, cancellationToken));
}
