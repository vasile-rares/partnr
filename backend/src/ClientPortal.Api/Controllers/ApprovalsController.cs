using ClientPortal.Application.DTOs.Approvals;
using ClientPortal.Application.Interfaces.Services;
using Microsoft.AspNetCore.Mvc;

namespace ClientPortal.Api.Controllers;

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
}
