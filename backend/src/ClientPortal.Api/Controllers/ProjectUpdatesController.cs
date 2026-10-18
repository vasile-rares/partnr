using ClientPortal.Application.DTOs.ProjectUpdates;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/updates")]
public sealed class ProjectUpdatesController(IProjectUpdateService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> Create(
        Guid workspaceId,
        Guid projectId,
        SaveProjectUpdateRequest request,
        CancellationToken cancellationToken)
    {
        var update = await service.CreateAsync(workspaceId, projectId, request, cancellationToken);
        return CreatedAtAction(nameof(GetById), new { workspaceId, projectId, id = update.Id }, update);
    }

    [HttpGet]
    public async Task<IActionResult> List(
        Guid workspaceId,
        Guid projectId,
        [FromQuery] bool includeArchived,
        CancellationToken cancellationToken) =>
        Ok(await service.ListAsync(workspaceId, projectId, includeArchived, cancellationToken));

    [HttpGet("{id:guid}")]
    public async Task<IActionResult> GetById(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.GetByIdAsync(workspaceId, projectId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveProjectUpdateRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.UpdateAsync(workspaceId, projectId, id, request, cancellationToken));

    [HttpPost("{id:guid}/share")]
    public async Task<IActionResult> Share(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ShareAsync(workspaceId, projectId, id, cancellationToken));

    [HttpPost("{id:guid}/unshare")]
    public async Task<IActionResult> Unshare(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.UnshareAsync(workspaceId, projectId, id, cancellationToken));

    [HttpPost("{id:guid}/archive")]
    public async Task<IActionResult> Archive(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.ArchiveAsync(workspaceId, projectId, id, cancellationToken));

    [HttpPost("{id:guid}/restore")]
    public async Task<IActionResult> Restore(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        Ok(await service.RestoreAsync(workspaceId, projectId, id, cancellationToken));

    [HttpDelete("{id:guid}")]
    public async Task<IActionResult> Delete(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken)
    {
        await service.DeleteAsync(workspaceId, projectId, id, cancellationToken);
        return NoContent();
    }
}
