using ClientPortal.Application.Authorization;
using ClientPortal.Application.DTOs.Approvals;
using ClientPortal.Application.DTOs.Deliverables;
using ClientPortal.Application.Exceptions;
using ClientPortal.Application.Interfaces.Repositories;
using ClientPortal.Application.Interfaces.Services;
using ClientPortal.Application.Mappings;
using ClientPortal.Domain.Entities;
using ClientPortal.Domain.Enums;

namespace ClientPortal.Application.Services;

internal sealed class ApprovalService(
    IApprovalRequestRepository approvals,
    IDeliverableRepository deliverables,
    IProjectRepository projects,
    DeliverableResponseBuilder responses,
    WorkspaceAccess access,
    IUnitOfWork unitOfWork) : IApprovalService
{
    public async Task<DeliverableResponse> RequestAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        RequestApprovalRequest request,
        CancellationToken cancellationToken)
    {
        var membership = await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, deliverableId, cancellationToken);

        if (deliverable.IsArchived)
        {
            throw new ConflictException("This deliverable is archived. Restore it before making changes.");
        }

        if (deliverable.Status != DeliverableStatus.Shared)
        {
            throw new ConflictException("Share the deliverable with the client before asking for their approval.");
        }

        if (await approvals.GetPendingAsync(deliverable.Id, cancellationToken) is not null)
        {
            throw new ConflictException("This deliverable already has an approval request waiting for the client.");
        }

        approvals.Add(ApprovalRequest.Request(workspaceId, projectId, deliverable.Id, membership.UserId, request.Note));
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    public async Task<DeliverableResponse> WithdrawAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        Guid approvalId,
        CancellationToken cancellationToken)
    {
        await access.RequireMemberAsync(workspaceId, cancellationToken);
        var project = await RequireEditableProjectAsync(workspaceId, projectId, cancellationToken);
        var deliverable = await RequireDeliverableAsync(workspaceId, projectId, deliverableId, cancellationToken);
        var approval = await approvals.GetByIdAsync(workspaceId, projectId, deliverableId, approvalId, cancellationToken)
            ?? throw new NotFoundException($"The approval request with id '{approvalId}' was not found.");

        if (!approval.IsPending)
        {
            throw new ConflictException("This approval request is already closed.");
        }

        approval.Withdraw();
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return await responses.BuildAsync(deliverable, project, cancellationToken);
    }

    private async Task<Project> RequireEditableProjectAsync(Guid workspaceId, Guid projectId, CancellationToken cancellationToken)
    {
        var project = await projects.GetByIdAsync(workspaceId, projectId, cancellationToken)
            ?? throw new NotFoundException($"The project with id '{projectId}' was not found.");

        if (project.IsArchived)
        {
            throw new ConflictException("This project is archived. Restore it before making changes.");
        }

        return project;
    }

    private async Task<Deliverable> RequireDeliverableAsync(
        Guid workspaceId,
        Guid projectId,
        Guid deliverableId,
        CancellationToken cancellationToken) =>
        await deliverables.GetByIdAsync(workspaceId, projectId, deliverableId, cancellationToken)
            ?? throw new NotFoundException($"The deliverable with id '{deliverableId}' was not found.");
}
