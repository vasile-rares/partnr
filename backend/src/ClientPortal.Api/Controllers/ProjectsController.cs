using ClientPortal.Application.DTOs.Projects;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/projects")]
public sealed class ProjectsController(IProjectService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(Guid workspaceId, CreateProjectRequest request, CancellationToken cancellationToken)
    {
        var project = await service.CreateAsync(workspaceId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { workspaceId, id = project.Id }, project);
    }

    [HttpGet]
    // includeArchived is false and clientId null when the query string leaves them out.
    public async Task<IActionResult> List(
        Guid workspaceId,
        [FromQuery] bool includeArchived,
        [FromQuery] Guid? clientId,
        CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, includeArchived, clientId, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(workspaceId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(Guid workspaceId, Guid id, UpdateProjectRequest request, CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(workspaceId, id, request, cancellationToken));

    // Archive / restore are actions, not field edits, so they get their own endpoints instead of a flag in PUT.
    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ArchiveAsync(workspaceId, id, cancellationToken));

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid workspaceId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RestoreAsync(workspaceId, id, cancellationToken));
}
