using ClientPortal.Api.Extensions;
using ClientPortal.Application.DTOs.Approvals;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

[ApiController]
[Route("api/portal")]
public sealed class PortalController(IPortalService service, IPortalApprovalService approvals) : ControllerBase
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

    [HttpGet("projects/{projectId:guid}/files/{fileId:guid}/download")]
    public async Task<IActionResult> DownloadProjectFile(Guid projectId, Guid fileId, CancellationToken cancellationToken) =>
        this.DownloadFile(await service.DownloadFileAsync(projectId, fileId, cancellationToken));

    [HttpPost("projects/{projectId:guid}/deliverables/{deliverableId:guid}/approvals/{approvalId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        ApproveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await approvals.ApproveAsync(projectId, deliverableId, approvalId, request, cancellationToken));

    [HttpPost("projects/{projectId:guid}/deliverables/{deliverableId:guid}/approvals/{approvalId:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        RequestChangesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await approvals.RequestChangesAsync(projectId, deliverableId, approvalId, request, cancellationToken));
}
