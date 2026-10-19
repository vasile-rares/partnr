using ClientPortal.Api.Extensions;
using ClientPortal.Application.DTOs.ProjectFiles;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Domain.Entities;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/files")]
public sealed class ProjectFilesController(IProjectFileService service) : ControllerBase
{
    // The multipart envelope is a little bigger than the file; the exact limit is checked on the file itself.
    private const long MaxRequestBytes = ProjectFile.MaxSizeBytes + 1024 * 1024;

    [HttpPost]
    [RequestSizeLimit(MaxRequestBytes)]
    [RequestFormLimits(MultipartBodyLengthLimit = MaxRequestBytes)]
    public async Task<IActionResult> Create(
        Guid workspaceId,
        Guid projectId,
        [FromForm] UploadProjectFileRequest request,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var created = await service.CreateAsync(
            workspaceId,
            projectId,
            request,
            new FileUpload(file.FileName, file.Length, content),
            cancellationToken);

        return CreatedAtAction(nameof(GetById), new { workspaceId, projectId, id = created.Id }, created);
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

    [HttpGet("{id:guid}/download")]
    public async Task<IActionResult> Download(Guid workspaceId, Guid projectId, Guid id, CancellationToken cancellationToken) =>
        this.DownloadFile(await service.DownloadAsync(workspaceId, projectId, id, cancellationToken));

    [HttpPut("{id:guid}")]
    public async Task<IActionResult> Update(
        Guid workspaceId,
        Guid projectId,
        Guid id,
        SaveProjectFileRequest request,
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
