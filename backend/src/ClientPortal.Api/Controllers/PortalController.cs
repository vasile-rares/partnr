using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

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
