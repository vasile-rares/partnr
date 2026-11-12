using Partnr.Application.DTOs.Approvals;
using Partnr.Application.DTOs.Deliverables;

namespace Partnr.Application.Interfaces.Services;

/// <summary>The workspace's side of approvals: asking the client, and taking the request back. Each call answers with the whole deliverable.</summary>
public interface IApprovalService
{
    Task<DeliverableResponse> RequestAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        RequestApprovalRequest request,
        CancellationToken cancellationToken);

    Task<DeliverableResponse> WithdrawAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        CancellationToken cancellationToken);
}
