using Partnr.Application.DTOs.Approvals;
using Partnr.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace Partnr.Api.Controllers;

[ApiController]
[Route("api/workspaces/{workspaceId:guid}/projects/{projectId:guid}/deliverables/{deliverableId:guid}/approvals")]
public sealed class ApprovalsController(IApprovalService service) : ControllerBase
{
    [HttpPost]
    public async Task<IActionResult> RequestApproval(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        RequestApprovalRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.RequestAsync(workspaceId, projectId, deliverableId, request, cancellationToken));

    [HttpPost("{approvalId:guid}/withdraw")]
    public async Task<IActionResult> Withdraw(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        CancellationToken cancellationToken) =>
        Ok(await service.WithdrawAsync(workspaceId, projectId, deliverableId, approvalId, cancellationToken));

    // The client's answer: closed to members, which get 404 like anyone else without the client's door.
    [HttpPost("{approvalId:guid}/approve")]
    public async Task<IActionResult> Approve(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        ApproveRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.ApproveAsync(workspaceId, projectId, deliverableId, approvalId, request, cancellationToken));

    [HttpPost("{approvalId:guid}/request-changes")]
    public async Task<IActionResult> RequestChanges(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        RequestChangesRequest request,
        CancellationToken cancellationToken) =>
        Ok(await service.RequestChangesAsync(workspaceId, projectId, deliverableId, approvalId, request, cancellationToken));
}
